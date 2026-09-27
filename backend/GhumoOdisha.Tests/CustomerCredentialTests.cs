using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

/// <summary>Google sign-in, adding a WhatsApp number later, PIN and email+password sign-in.</summary>
public class CustomerCredentialTests
{
    private static string RandomEmail() => $"test-{Guid.NewGuid():N}@gmail.com";

    [Fact]
    public async Task GoogleSignIn_NewUser_CreatesAccountWithoutPhone()
    {
        await using var db = TestDb.CreateContext();
        var google = new FakeGoogleTokenValidator();
        var service = TestServices.CreateCustomerAuthService(db, new FakeWhatsAppService(), fakeGoogle: google);
        var email = RandomEmail();
        var token = google.Issue(email: email.ToUpperInvariant(), name: "Priya Sahoo");

        var auth = await service.GoogleSignInAsync(new GoogleSignInRequest(token));

        Assert.Null(auth.PhoneNumber);
        Assert.Equal(email, auth.Email); // stored lower-cased
        Assert.Equal("Priya Sahoo", auth.Name);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));

        var customer = await db.Customers.SingleAsync(c => c.CustomerId == auth.CustomerId);
        Assert.True(customer.EmailVerified);
        Assert.Equal(google.IdentityFor(token).Subject, customer.GoogleSubject);
    }

    [Fact]
    public async Task GoogleSignIn_SecondTime_ReturnsSameAccount()
    {
        await using var db = TestDb.CreateContext();
        var google = new FakeGoogleTokenValidator();
        var service = TestServices.CreateCustomerAuthService(db, new FakeWhatsAppService(), fakeGoogle: google);
        var first = google.Issue(email: RandomEmail());
        var subject = google.IdentityFor(first).Subject;
        var second = google.Issue(email: google.IdentityFor(first).Email, subject: subject);

        var a = await service.GoogleSignInAsync(new GoogleSignInRequest(first));
        var b = await service.GoogleSignInAsync(new GoogleSignInRequest(second));

        Assert.Equal(a.CustomerId, b.CustomerId);
    }

    [Fact]
    public async Task GoogleSignIn_InvalidToken_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var service = TestServices.CreateCustomerAuthService(db, new FakeWhatsAppService());

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.GoogleSignInAsync(new GoogleSignInRequest("forged-token")));
    }

    [Fact]
    public async Task GoogleSignIn_EmailAlreadyTypedIntoAnotherAccount_IsRejectedNotMerged()
    {
        await using var db = TestDb.CreateContext();
        var email = RandomEmail();
        var now = DateTime.UtcNow;
        var existing = new Customer { Name = "Someone", PhoneNumber = TestDb.RandomPhoneNumber(), Email = email, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        var google = new FakeGoogleTokenValidator();
        var service = TestServices.CreateCustomerAuthService(db, new FakeWhatsAppService(), fakeGoogle: google);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.GoogleSignInAsync(new GoogleSignInRequest(google.Issue(email: email))));

        await db.Entry(existing).ReloadAsync();
        Assert.Null(existing.GoogleSubject);
    }

    [Fact]
    public async Task GoogleUser_AddsPhoneWithOtp_ThenCanSignInWithWhatsAppOtp()
    {
        await using var db = TestDb.CreateContext();
        var google = new FakeGoogleTokenValidator();
        var whatsApp = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, whatsApp, fakeGoogle: google);
        var auth = await service.GoogleSignInAsync(new GoogleSignInRequest(google.Issue(email: RandomEmail())));
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestAddPhoneOtpAsync(auth.CustomerId, new AddPhoneRequest(phone));
        Assert.Equal(phone, whatsApp.LastPhoneNumber);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.VerifyAddPhoneOtpAsync(auth.CustomerId, new VerifyOtpRequest(phone, whatsApp.LastOtp == "000000" ? "111111" : "000000")));
        await service.VerifyAddPhoneOtpAsync(auth.CustomerId, new VerifyOtpRequest(phone, whatsApp.LastOtp!));

        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == auth.CustomerId);
        Assert.Equal(phone, customer.PhoneNumber);

        // The same account now also opens with a WhatsApp code (fresh service = no resend cooldown).
        var later = TestServices.CreateCustomerAuthService(db, whatsApp, fakeGoogle: google);
        await later.RequestOtpAsync(new RequestOtpRequest(null, phone));
        var viaOtp = await later.VerifyOtpAsync(new VerifyOtpRequest(phone, whatsApp.LastOtp!));
        Assert.Equal(auth.CustomerId, viaOtp.CustomerId);
    }

    [Fact]
    public async Task AddPhone_NumberOwnedByAnotherAccount_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var google = new FakeGoogleTokenValidator();
        var whatsApp = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, whatsApp, fakeGoogle: google);
        var phone = TestDb.RandomPhoneNumber();
        var now = DateTime.UtcNow;
        db.Customers.Add(new Customer { Name = "Owner", PhoneNumber = phone, IsVerified = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var auth = await service.GoogleSignInAsync(new GoogleSignInRequest(google.Issue(email: RandomEmail())));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RequestAddPhoneOtpAsync(auth.CustomerId, new AddPhoneRequest(phone)));
        Assert.Equal(0, whatsApp.CallCount);
    }

    [Fact]
    public async Task WhatsAppUser_LinksGoogle_ThenCanSignInWithGoogle()
    {
        await using var db = TestDb.CreateContext();
        var google = new FakeGoogleTokenValidator();
        var whatsApp = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, whatsApp, fakeGoogle: google);
        var phone = TestDb.RandomPhoneNumber();
        await service.RequestOtpAsync(new RequestOtpRequest("Rahul", phone));
        var otpAuth = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, whatsApp.LastOtp!));

        var linkToken = google.Issue(email: RandomEmail());
        await service.LinkGoogleAsync(otpAuth.CustomerId, new GoogleSignInRequest(linkToken));

        var identity = google.IdentityFor(linkToken);
        var signIn = await service.GoogleSignInAsync(new GoogleSignInRequest(
            google.Issue(email: identity.Email, subject: identity.Subject)));

        Assert.Equal(otpAuth.CustomerId, signIn.CustomerId);
        Assert.Equal(phone, signIn.PhoneNumber);
    }

    [Fact]
    public async Task LinkGoogle_AlreadyLinkedElsewhere_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var google = new FakeGoogleTokenValidator();
        var whatsApp = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, whatsApp, fakeGoogle: google);

        var googleToken = google.Issue(email: RandomEmail());
        await service.GoogleSignInAsync(new GoogleSignInRequest(googleToken));

        var phone = TestDb.RandomPhoneNumber();
        await service.RequestOtpAsync(new RequestOtpRequest("Other", phone));
        var other = await service.VerifyOtpAsync(new VerifyOtpRequest(phone, whatsApp.LastOtp!));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.LinkGoogleAsync(other.CustomerId, new GoogleSignInRequest(googleToken)));
    }
}
