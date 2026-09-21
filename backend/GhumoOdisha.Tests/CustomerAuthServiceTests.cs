using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

public class CustomerAuthServiceTests
{
    [Fact]
    public async Task RequestOtp_ForNewCustomer_NameIsOptional_CreatesCustomerWithBlankName()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        // Sign-in by phone number alone must succeed for a brand-new customer — the app asks for
        // a name later, before their first booking, rather than gating sign-in on it.
        await service.RequestOtpAsync(new RequestOtpRequest(null, phone));
        var authResult = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        Assert.Equal("", authResult.Name);

        var customer = await db.Customers.SingleAsync(c => c.PhoneNumber == phone);
        Assert.Equal("", customer.Name);
    }

    [Fact]
    public async Task RequestOtpThenVerify_NewCustomer_CreatesCustomerAndIssuesTokens()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        var requestResult = await service.RequestOtpAsync(new RequestOtpRequest("Rahul Das", phone));

        Assert.Equal(TestServices.OtpSettings.Length, requestResult.OtpLength);
        Assert.Equal("Rahul Das", fake.LastCustomerName);
        Assert.NotNull(fake.LastOtp);
        Assert.Equal(1, fake.CallCount);

        var authResult = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        Assert.Equal("Rahul Das", authResult.Name);
        Assert.Equal(phone, authResult.PhoneNumber);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.False(string.IsNullOrWhiteSpace(authResult.RefreshToken));

        var customer = await db.Customers.SingleAsync(c => c.PhoneNumber == phone);
        Assert.Equal("Rahul Das", customer.Name);
        Assert.True(customer.IsVerified);
        Assert.Null(customer.PinHash);
    }

    [Fact]
    public async Task RequestOtp_ForExistingCustomer_UsesStoredNameAndNeverOverwritesIt()
    {
        await using var db = TestDb.CreateContext();
        var phone = TestDb.RandomPhoneNumber();
        var now = DateTime.UtcNow;
        db.Customers.Add(new Customer
        {
            Name = "Original Name",
            PhoneNumber = phone,
            IsVerified = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var fake = new FakeFast2SmsWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);

        // No name supplied — an existing customer's name must never be overwritten with blank.
        await service.RequestOtpAsync(new RequestOtpRequest(null, phone));

        Assert.Equal("Original Name", fake.LastCustomerName);

        await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        var customer = await db.Customers.SingleAsync(c => c.PhoneNumber == phone);
        Assert.Equal("Original Name", customer.Name);
    }

    [Fact]
    public async Task Resend_InvalidatesThePreviousOtp()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        // Cooldown disabled here specifically to isolate "resend invalidates the prior OTP"
        // from the resend-cooldown behavior (which is a separate, deliberate rate limit).
        var otpSettings = new OtpSettings { Length = 6, ExpiryMinutes = 5, MaxAttempts = 5, ResendCooldownSeconds = 0, MaxRequestsPerHour = 5 };
        var service = TestServices.CreateCustomerAuthService(db, fake, otpSettings);
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestOtpAsync(new RequestOtpRequest("Test User", phone));
        var firstOtp = fake.LastOtp!;

        await service.RequestOtpAsync(new RequestOtpRequest("Test User", phone));
        var secondOtp = fake.LastOtp!;

        // The first OTP must no longer verify once a second one has been issued.
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest(phone, firstOtp)));

        var authResult = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, secondOtp));
        Assert.NotNull(authResult.Token);
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_IncrementsAttemptsThenLocksOutAfterMax()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        var otpSettings = new OtpSettings { Length = 6, ExpiryMinutes = 5, MaxAttempts = 2, ResendCooldownSeconds = 30, MaxRequestsPerHour = 5 };
        var service = TestServices.CreateCustomerAuthService(db, fake, otpSettings);
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestOtpAsync(new RequestOtpRequest("Test User", phone));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest(phone, "000000")));
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest(phone, "000000")));

        // Third attempt (correct or not) is rejected purely on attempt count.
        await Assert.ThrowsAsync<ConflictException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!)));
    }

    [Fact]
    public async Task VerifyOtp_Expired_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestOtpAsync(new RequestOtpRequest("Test User", phone));
        var otp = fake.LastOtp!;

        var record = await db.CustomerOtps.SingleAsync(o => o.PhoneNumber == phone && !o.IsUsed);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.VerifyOtpAsync(new VerifyOtpRequest(phone, otp)));
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndRevokesThePrevious()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestOtpAsync(new RequestOtpRequest("Test User", phone));
        var authResult = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        var refreshed = await service.RefreshAsync(new RefreshTokenRequest(authResult.RefreshToken));

        Assert.NotEqual(authResult.RefreshToken, refreshed.RefreshToken);
        Assert.Equal(authResult.CustomerId, refreshed.CustomerId);

        // The original refresh token must now be revoked (rotation).
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RefreshAsync(new RefreshTokenRequest(authResult.RefreshToken)));
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestOtpAsync(new RequestOtpRequest("Test User", phone));
        var authResult = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        await service.LogoutAsync(new LogoutRequest(authResult.RefreshToken));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.RefreshAsync(new RefreshTokenRequest(authResult.RefreshToken)));
    }

    [Fact]
    public async Task SendOtp_ProviderFailure_DoesNotCreateAnyBookingOrSession()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeFast2SmsWhatsAppService { ShouldFail = true };
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        await Assert.ThrowsAsync<WhatsAppDeliveryException>(
            () => service.RequestOtpAsync(new RequestOtpRequest("Test User", phone)));

        var customerExists = await db.Customers.AnyAsync(c => c.PhoneNumber == phone);
        Assert.False(customerExists);
    }
}
