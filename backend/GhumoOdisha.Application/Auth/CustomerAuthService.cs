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

/// <summary>
/// Customer sign-in — two ways in, both ending in the same JWT + refresh-token session:
/// WhatsApp OTP and Google. A Google-only customer can add (and OTP-verify) a WhatsApp number
/// later; a WhatsApp customer can link Google from their profile.
/// </summary>
public class CustomerAuthService(
    IGhumoOdishaDbContext db,
    IPinHasher pinHasher,
    IJwtTokenService jwtTokenService,
    IWhatsAppService whatsAppService,
    IGoogleTokenValidator googleTokenValidator,
    IOptions<OtpSettings> otpOptions,
    IOptions<JwtSettings> jwtOptions,
    IMemoryCache cache,
    ILogger<CustomerAuthService> logger) : ICustomerAuthService
{
    private readonly OtpSettings _otpSettings = otpOptions.Value;
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    // ---------- WhatsApp OTP ----------

    public async Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = NormalizeValidPhone(request.WhatsAppNumber);

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone, cancellationToken);

        // New users can sign in with just a phone number — no name required at signup. The app
        // asks for a name later, before the customer's first booking, if one still isn't on file.
        var nameToSend = customer is not null
            ? customer.Name
            : (request.Name?.Trim() ?? "");

        return await IssueOtpAsync(phone, nameToSend, cancellationToken);
    }

    public async Task<CustomerAuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.WhatsAppNumber);
        var record = await ConsumeOtpAsync(phone, request.Otp, cancellationToken);
        var now = DateTime.UtcNow;

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

        return await StartSessionAsync(customer, now, cancellationToken);
    }

    // ---------- Google ----------

    public async Task<CustomerAuthResponse> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken cancellationToken = default)
    {
        var identity = await ValidateGoogleAsync(request.Credential, cancellationToken);
        var email = NormalizeEmail(identity.Email);
        var now = DateTime.UtcNow;

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.GoogleSubject == identity.Subject, cancellationToken);
        if (customer is null)
        {
            if (!identity.EmailVerified)
            {
                throw new InvalidCredentialsException(
                    "Google hasn't verified this account's email. Please sign in with WhatsApp instead.");
            }
            customer = await FindOrCreateByProvenEmailAsync(email, identity.Name, identity.Subject, now, cancellationToken);
        }
        else
        {
            // Adopt the Google address only if the account has no confirmed email of its own yet.
            if (identity.EmailVerified && !customer.EmailVerified && !await IsEmailVerifiedElsewhereAsync(email, customer.CustomerId, cancellationToken))
            {
                customer.Email = email;
                customer.EmailVerified = true;
            }
            if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(identity.Name))
            {
                customer.Name = identity.Name.Trim();
            }
            customer.LastLoginAt = now;
            customer.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await StartSessionAsync(customer, now, cancellationToken);
    }

    /// <summary>
    /// Google sign-in for an address Google has just proven the person owns:
    /// the account with that confirmed email, else a new account. An account that merely has the
    /// address typed in (unconfirmed) is never taken over — that would let anyone pre-claim a
    /// stranger's email — and no duplicate is created; its owner signs in with WhatsApp and links Google from Profile.
    /// Saving is left to the caller.
    /// </summary>
    private async Task<Customer> FindOrCreateByProvenEmailAsync(string email, string? name, string? googleSubject, DateTime now, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email && c.EmailVerified, cancellationToken);
        if (customer is not null)
        {
            if (googleSubject is not null)
            {
                if (customer.GoogleSubject is not null && customer.GoogleSubject != googleSubject)
                {
                    throw new ConflictException("This email is linked to a different Google account.");
                }
                // A Google-verified email on file proves the same address — safe to link.
                customer.GoogleSubject = googleSubject;
            }
            if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(name))
            {
                customer.Name = name.Trim();
            }
            customer.LastLoginAt = now;
            customer.UpdatedAt = now;
            return customer;
        }

        if (await db.Customers.AnyAsync(c => c.Email == email, cancellationToken))
        {
            throw new ConflictException(
                "This email is on an account that hasn't confirmed it yet. Sign in with WhatsApp, then link Google from your Profile.");
        }

        customer = new Customer
        {
            // Blank when unknown — the app asks for a name before the first booking.
            Name = name?.Trim() ?? "",
            Email = email,
            EmailVerified = true,
            GoogleSubject = googleSubject,
            IsVerified = true,
            FailedLoginAttempts = 0,
            CreatedAt = now,
            UpdatedAt = now,
            LastLoginAt = now
        };
        db.Customers.Add(customer);
        logger.LogInformation("New customer account created via {Method} sign-in.", "Google");
        return customer;
    }

    // ---------- Signed-in account management ----------

    public async Task<RequestOtpResponse> RequestAddPhoneOtpAsync(int customerId, AddPhoneRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var phone = NormalizeValidPhone(request.WhatsAppNumber);

        if (customer.PhoneNumber == phone)
        {
            throw new ConflictException("This number is already on your account.");
        }

        await EnsurePhoneFreeAsync(phone, customerId, cancellationToken);
        return await IssueOtpAsync(phone, customer.Name, cancellationToken);
    }

    public async Task VerifyAddPhoneOtpAsync(int customerId, VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var phone = PhoneNumberNormalizer.Normalize(request.WhatsAppNumber);

        // Checked again here: the number may have been registered since the OTP was sent.
        await EnsurePhoneFreeAsync(phone, customerId, cancellationToken);
        await ConsumeOtpAsync(phone, request.Otp, cancellationToken);

        var now = DateTime.UtcNow;
        customer.PhoneNumber = phone;
        customer.IsVerified = true;
        customer.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index on PhoneNumber lost a race with another sign-up for the same number.
            throw new PhoneAlreadyRegisteredException();
        }
    }

    public async Task LinkGoogleAsync(int customerId, GoogleSignInRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await GetCustomerAsync(customerId, cancellationToken);
        var identity = await ValidateGoogleAsync(request.Credential, cancellationToken);

        var owner = await db.Customers.FirstOrDefaultAsync(c => c.GoogleSubject == identity.Subject, cancellationToken);
        if (owner is not null && owner.CustomerId != customerId)
        {
            throw new ConflictException("This Google account is already linked to a different Ghumo Odisha account.");
        }

        customer.GoogleSubject = identity.Subject;

        // Take the Google address only if the account has no confirmed email yet, and no other
        // account has already confirmed it.
        var googleEmail = NormalizeEmail(identity.Email);
        if (identity.EmailVerified && !customer.EmailVerified)
        {
            if (await IsEmailVerifiedElsewhereAsync(googleEmail, customerId, cancellationToken))
            {
                throw new ConflictException("This Google account's email belongs to a different Ghumo Odisha account.");
            }
            customer.Email = googleEmail;
            customer.EmailVerified = true;
        }
        if (string.IsNullOrWhiteSpace(customer.Name) && !string.IsNullOrWhiteSpace(identity.Name))
        {
            customer.Name = identity.Name.Trim();
        }
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Session ----------

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
        return ToAuthResponse(record.Customer, accessToken, newPlain);
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

    // ---------- Helpers ----------

    private async Task<CustomerAuthResponse> StartSessionAsync(Customer customer, DateTime now, CancellationToken cancellationToken)
    {
        var accessToken = jwtTokenService.GenerateCustomerToken(customer);
        var (refreshTokenPlain, refreshTokenEntity) = CreateRefreshToken(customer.CustomerId, now);
        db.CustomerRefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync(cancellationToken);

        return ToAuthResponse(customer, accessToken, refreshTokenPlain);
    }

    private static CustomerAuthResponse ToAuthResponse(Customer customer, string token, string refreshToken) =>
        new(token, refreshToken, customer.CustomerId, customer.Name, customer.PhoneNumber, customer.Email);

    private async Task<GoogleIdentity> ValidateGoogleAsync(string credential, CancellationToken cancellationToken)
    {
        var identity = await googleTokenValidator.ValidateAsync(credential, cancellationToken);
        if (identity is null || string.IsNullOrWhiteSpace(identity.Email))
        {
            throw new InvalidCredentialsException("Google sign-in failed. Please try again.");
        }
        return identity;
    }

    private async Task<Customer> GetCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

    private async Task EnsurePhoneFreeAsync(string phone, int customerId, CancellationToken cancellationToken)
    {
        var taken = await db.Customers.AnyAsync(c => c.PhoneNumber == phone && c.CustomerId != customerId, cancellationToken);
        if (taken)
        {
            throw new ConflictException(
                "This WhatsApp number already has an account. Sign in with it instead, then link Google from your Profile.");
        }
    }

    private static string NormalizeValidPhone(string raw)
    {
        var phone = PhoneNumberNormalizer.Normalize(raw);
        if (!Regex.IsMatch(phone, "^[6-9][0-9]{9}$"))
        {
            throw new ValidationAppException(["Enter a valid 10-digit WhatsApp number."]);
        }
        return phone;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private Task<bool> IsEmailVerifiedElsewhereAsync(string email, int customerId, CancellationToken cancellationToken) =>
        db.Customers.AnyAsync(c => c.Email == email && c.EmailVerified && c.CustomerId != customerId, cancellationToken);

    private async Task<RequestOtpResponse> IssueOtpAsync(string phone, string name, CancellationToken cancellationToken)
    {
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
            Name = name,
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
            var greetingName = string.IsNullOrWhiteSpace(name) ? "there" : name;
            await whatsAppService.SendOtpAsync(phone, greetingName, otp, cancellationToken);
        }

        return new RequestOtpResponse(_otpSettings.Length);
    }

    /// <summary>Checks the latest unused OTP for the phone and marks it used. Throws on any failure.</summary>
    private async Task<CustomerOtp> ConsumeOtpAsync(string phone, string otp, CancellationToken cancellationToken)
    {
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

        if (!pinHasher.Verify(record.OtpHash, otp))
        {
            record.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidCredentialsException("Invalid OTP.");
        }

        record.IsUsed = true;
        record.UsedAt = now;
        return record;
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
