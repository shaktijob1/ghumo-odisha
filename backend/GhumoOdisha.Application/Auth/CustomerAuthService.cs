using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Auth;

public class CustomerAuthService(
    IGhumoOdishaDbContext db,
    IPinHasher pinHasher,
    IJwtTokenService jwtTokenService,
    IWhatsAppService whatsAppService,
    IOptions<OtpSettings> otpOptions,
    IOptions<JwtSettings> jwtOptions,
    IMemoryCache cache,
    ILogger<CustomerAuthService> logger) : ICustomerAuthService
{
    private readonly OtpSettings _otpSettings = otpOptions.Value;
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    public async Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.WhatsAppNumber);
        if (!Regex.IsMatch(phone, "^[6-9][0-9]{9}$"))
        {
            throw new ValidationAppException(["Enter a valid 10-digit WhatsApp number."]);
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, cancellationToken);

        // New users can sign in with just a phone number — no name required at signup. The app
        // asks for a name later, before the customer's first booking, if one still isn't on file.
        var nameToSend = customer is not null
            ? customer.Name
            : (request.Name?.Trim() ?? "");

        EnsureCanRequestOtp(phone);

        var now = DateTime.UtcNow;

        var previousOtps = await db.CustomerOtps
            .Where(o => o.PhoneNumber == phone && !o.IsUsed)
            .ToListAsync(cancellationToken);
        foreach (var previous in previousOtps)
        {
            previous.IsUsed = true;
        }

        var devBypass = _otpSettings.DevBypassEnabled;
        var otp = devBypass ? new string('0', _otpSettings.Length) : GenerateOtp(_otpSettings.Length);

        db.CustomerOtps.Add(new CustomerOtp
        {
            PhoneNumber = phone,
            Name = nameToSend,
            OtpHash = pinHasher.Hash(otp),
            ExpiresAt = now.AddMinutes(_otpSettings.ExpiryMinutes),
            AttemptCount = 0,
            IsUsed = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);

        MarkOtpRequested(phone);

        if (devBypass)
        {
            logger.LogWarning("DEV OTP BYPASS active — skipping WhatsApp, OTP for {Phone} is {Otp}", phone, otp);
        }
        else
        {
            // The template greets by name — fall back to something generic rather than sending a blank.
            var greetingName = string.IsNullOrWhiteSpace(nameToSend) ? "there" : nameToSend;
            await whatsAppService.SendOtpAsync(phone, greetingName, otp, cancellationToken);
        }

        return new RequestOtpResponse(_otpSettings.Length);
    }

    public async Task<CustomerAuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.WhatsAppNumber);

        var record = await db.CustomerOtps
            .Where(o => o.PhoneNumber == phone && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is null)
        {
            throw new InvalidCredentialsException("OTP expired or not found. Please request a new one.");
        }

        var now = DateTime.UtcNow;
        if (record.ExpiresAt <= now)
        {
            throw new InvalidCredentialsException("OTP expired. Please request a new one.");
        }

        if (record.AttemptCount >= _otpSettings.MaxAttempts)
        {
            throw new ConflictException("Too many attempts. Please request a new OTP.");
        }

        if (!pinHasher.Verify(record.OtpHash, request.Otp))
        {
            record.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException("Invalid OTP.");
        }

        record.IsUsed = true;
        record.UsedAt = now;

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, cancellationToken);
        if (customer is null)
        {
            customer = new Customer
            {
                // Left blank when the customer signed in with just a phone number — the frontend
                // prompts for a real name before their first booking when this is empty.
                Name = record.Name ?? "",
                PhoneNumber = phone,
                IsVerified = true,
                FailedLoginAttempts = 0,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now
            };
            db.Customers.Add(customer);
        }
        else
        {
            customer.IsVerified = true;
            customer.LastLoginAt = now;
            customer.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateCustomerToken(customer);
        var (refreshTokenPlain, refreshTokenEntity) = CreateRefreshToken(customer.CustomerId, now);
        db.CustomerRefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new CustomerAuthResponse(accessToken, refreshTokenPlain, customer.CustomerId, customer.Name, customer.PhoneNumber, customer.Email);
    }

    public async Task<CustomerAuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(request.RefreshToken);
        var now = DateTime.UtcNow;

        var record = await db.CustomerRefreshTokens
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        if (record is null || record.RevokedAt is not null || record.ExpiresAt <= now)
        {
            throw new InvalidCredentialsException("Session expired. Please sign in again.");
        }

        record.RevokedAt = now;

        var (newPlain, newEntity) = CreateRefreshToken(record.CustomerId, now);
        db.CustomerRefreshTokens.Add(newEntity);
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = jwtTokenService.GenerateCustomerToken(record.Customer);
        return new CustomerAuthResponse(accessToken, newPlain, record.Customer.CustomerId, record.Customer.Name, record.Customer.PhoneNumber, record.Customer.Email);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var hash = HashToken(request.RefreshToken);
        var record = await db.CustomerRefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);

        if (record is not null && record.RevokedAt is null)
        {
            record.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private (string Plain, CustomerRefreshToken Entity) CreateRefreshToken(int customerId, DateTime now)
    {
        var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var entity = new CustomerRefreshToken
        {
            CustomerId = customerId,
            TokenHash = HashToken(plain),
            ExpiresAt = now.AddDays(_jwtSettings.CustomerRefreshTokenLifetimeDays),
            CreatedAt = now
        };

        return (plain, entity);
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string GenerateOtp(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString().PadLeft(length, '0');
    }

    private void EnsureCanRequestOtp(string phoneNumber)
    {
        if (cache.TryGetValue($"otp-cooldown:{phoneNumber}", out _))
        {
            throw new ConflictException("Please wait before requesting another OTP.");
        }

        var count = cache.TryGetValue($"otp-hourly:{phoneNumber}", out int existing) ? existing : 0;
        if (count >= _otpSettings.MaxRequestsPerHour)
        {
            throw new TooManyAttemptsException();
        }
    }

    private void MarkOtpRequested(string phoneNumber)
    {
        if (_otpSettings.ResendCooldownSeconds > 0)
        {
            cache.Set($"otp-cooldown:{phoneNumber}", true, TimeSpan.FromSeconds(_otpSettings.ResendCooldownSeconds));
        }

        var count = cache.TryGetValue($"otp-hourly:{phoneNumber}", out int existing) ? existing : 0;
        cache.Set($"otp-hourly:{phoneNumber}", count + 1, TimeSpan.FromHours(1));
    }
}
