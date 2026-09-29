using System.Security.Cryptography;
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
/// WhatsApp OTP: sending a code to a number and checking it. Shared by customer and driver sign-in —
/// both only need proof that the person holds the number. Codes are hashed, attempts are capped and
/// requests are rate-limited per number (cooldown + hourly cap).
/// </summary>
public interface IPhoneOtpService
{
    Task<RequestOtpResponse> IssueAsync(string phone, string name, CancellationToken cancellationToken = default);

    /// <summary>Checks the latest unused OTP for the phone and marks it used (the caller saves). Throws on any failure.</summary>
    Task<CustomerOtp> ConsumeAsync(string phone, string otp, CancellationToken cancellationToken = default);
}

public class PhoneOtpService(
    IGhumoOdishaDbContext db,
    IPinHasher pinHasher,
    IWhatsAppService whatsAppService,
    IOptions<OtpSettings> otpOptions,
    IMemoryCache cache,
    ILogger<PhoneOtpService> logger) : IPhoneOtpService
{
    private readonly OtpSettings _otpSettings = otpOptions.Value;

    /// <summary>Normalises an Indian mobile number and rejects anything that isn't one.</summary>
    public static string NormalizeValidPhone(string raw)
    {
        var phone = PhoneNumberNormalizer.Normalize(raw);
        if (!Regex.IsMatch(phone, "^[6-9][0-9]{9}$"))
        {
            throw new ValidationAppException(["Enter a valid 10-digit WhatsApp number."]);
        }
        return phone;
    }

    public async Task<RequestOtpResponse> IssueAsync(string phone, string name, CancellationToken cancellationToken = default)
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

    public async Task<CustomerOtp> ConsumeAsync(string phone, string otp, CancellationToken cancellationToken = default)
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
