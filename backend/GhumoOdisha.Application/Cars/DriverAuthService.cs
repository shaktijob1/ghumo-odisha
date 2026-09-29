using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Cars;

public interface IDriverAuthService
{
    Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default);
    Task<DriverAuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    Task<DriverAuthResponse> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken cancellationToken = default);
    Task<RequestOtpResponse> RequestAddPhoneOtpAsync(int driverId, AddPhoneRequest request, CancellationToken cancellationToken = default);
    Task VerifyAddPhoneOtpAsync(int driverId, VerifyOtpRequest request, CancellationToken cancellationToken = default);
    Task<DriverAuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Driver sign-in — WhatsApp OTP or Google, the same two ways customers use (and the same OTP service),
/// but a separate Driver account with the "Driver" role. A first sign-in creates the account in
/// Pending; the driver then completes their profile and submits it for admin review.
/// </summary>
public class DriverAuthService(
    IGhumoOdishaDbContext db,
    IPhoneOtpService phoneOtp,
    IJwtTokenService jwtTokenService,
    IGoogleTokenValidator googleTokenValidator,
    IOptions<JwtSettings> jwtOptions,
    ILogger<DriverAuthService> logger) : IDriverAuthService
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    public async Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = PhoneOtpService.NormalizeValidPhone(request.WhatsAppNumber);
        var driver = await db.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.PhoneNumber == phone, cancellationToken);
        return await phoneOtp.IssueAsync(phone, driver?.Name ?? request.Name?.Trim() ?? "", cancellationToken);
    }

    public async Task<DriverAuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.WhatsAppNumber);
        var otp = await phoneOtp.ConsumeAsync(phone, request.Otp, cancellationToken);
        var now = DateTime.UtcNow;

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.PhoneNumber == phone, cancellationToken);
        if (driver is null)
        {
            driver = NewDriver(otp.Name ?? "", now);
            driver.PhoneNumber = phone;
            db.Drivers.Add(driver);
            logger.LogInformation("New driver account created via {Method} sign-in.", "WhatsApp");
        }
        else
        {
            driver.LastLoginAt = now;
            driver.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await StartSessionAsync(driver, now, cancellationToken);
    }

    public async Task<DriverAuthResponse> GoogleSignInAsync(GoogleSignInRequest request, CancellationToken cancellationToken = default)
    {
        var identity = await googleTokenValidator.ValidateAsync(request.Credential, cancellationToken);
        if (identity is null || string.IsNullOrWhiteSpace(identity.Email))
        {
            throw new InvalidCredentialsException("Google sign-in failed. Please try again.");
        }

        var email = identity.Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.GoogleSubject == identity.Subject, cancellationToken);
        if (driver is null)
        {
            if (!identity.EmailVerified)
            {
                throw new InvalidCredentialsException("Google hasn't verified this account's email. Please sign in with WhatsApp instead.");
            }

            // Only an address a driver already had confirmed by Google is matched — a typed-in email
            // on someone else's account is never taken over.
            driver = await db.Drivers.FirstOrDefaultAsync(d => d.Email == email && d.EmailVerified, cancellationToken);
            if (driver is null)
            {
                if (await db.Drivers.AnyAsync(d => d.Email == email, cancellationToken))
                {
                    throw new ConflictException("This email is on a driver account that hasn't confirmed it. Please sign in with WhatsApp.");
                }

                driver = NewDriver(identity.Name?.Trim() ?? "", now);
                db.Drivers.Add(driver);
                logger.LogInformation("New driver account created via {Method} sign-in.", "Google");
            }
            else if (driver.GoogleSubject is not null && driver.GoogleSubject != identity.Subject)
            {
                throw new ConflictException("This email is linked to a different Google account.");
            }
            driver.GoogleSubject = identity.Subject;
            driver.Email = email;
            driver.EmailVerified = true;
        }

        if (string.IsNullOrWhiteSpace(driver.Name) && !string.IsNullOrWhiteSpace(identity.Name))
        {
            driver.Name = identity.Name.Trim();
        }
        driver.LastLoginAt = now;
        driver.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return await StartSessionAsync(driver, now, cancellationToken);
    }

    public async Task<RequestOtpResponse> RequestAddPhoneOtpAsync(int driverId, AddPhoneRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(driverId, cancellationToken);
        var phone = PhoneOtpService.NormalizeValidPhone(request.WhatsAppNumber);
        if (driver.PhoneNumber == phone)
        {
            throw new ConflictException("This number is already on your account.");
        }

        await EnsurePhoneFreeAsync(phone, driverId, cancellationToken);
        return await phoneOtp.IssueAsync(phone, driver.Name, cancellationToken);
    }

    public async Task VerifyAddPhoneOtpAsync(int driverId, VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await GetDriverAsync(driverId, cancellationToken);
        var phone = PhoneNumberNormalizer.Normalize(request.WhatsAppNumber);

        await EnsurePhoneFreeAsync(phone, driverId, cancellationToken);
        await phoneOtp.ConsumeAsync(phone, request.Otp, cancellationToken);

        var old = driver.PhoneNumber;
        driver.PhoneNumber = phone;
        driver.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DriverPhoneChanged", "WhatsApp number changed", CarActor.Driver(driverId), old, phone);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            throw new PhoneAlreadyRegisteredException();
        }
    }

    public async Task<DriverAuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var hash = RefreshTokens.Hash(request.RefreshToken);
        var now = DateTime.UtcNow;

        var record = await db.DriverRefreshTokens.Include(r => r.Driver).FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
        if (record is null || record.RevokedAt is not null || record.ExpiresAt <= now)
        {
            throw new InvalidCredentialsException("Session expired. Please sign in again.");
        }

        record.RevokedAt = now;
        return await StartSessionAsync(record.Driver, now, cancellationToken);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var hash = RefreshTokens.Hash(request.RefreshToken);
        var record = await db.DriverRefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
        if (record is not null && record.RevokedAt is null)
        {
            record.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static Driver NewDriver(string name, DateTime now) => new()
    {
        Name = name,
        Status = DriverStatus.Pending,
        CreatedAt = now,
        UpdatedAt = now,
        LastLoginAt = now
    };

    private async Task<DriverAuthResponse> StartSessionAsync(Driver driver, DateTime now, CancellationToken cancellationToken)
    {
        var plain = RefreshTokens.NewPlain();
        db.DriverRefreshTokens.Add(new DriverRefreshToken
        {
            DriverId = driver.DriverId,
            Driver = driver,
            TokenHash = RefreshTokens.Hash(plain),
            ExpiresAt = now.AddDays(_jwtSettings.CustomerRefreshTokenLifetimeDays),
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);

        return new DriverAuthResponse(jwtTokenService.GenerateDriverToken(driver), plain, driver.DriverId, driver.Name,
            driver.PhoneNumber, driver.Email, driver.Status);
    }

    private async Task<Driver> GetDriverAsync(int driverId, CancellationToken cancellationToken) =>
        await db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");

    private async Task EnsurePhoneFreeAsync(string phone, int driverId, CancellationToken cancellationToken)
    {
        if (await db.Drivers.AnyAsync(d => d.PhoneNumber == phone && d.DriverId != driverId, cancellationToken))
        {
            throw new ConflictException("This WhatsApp number is already on another driver account.");
        }
    }
}
