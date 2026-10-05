using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Tests.Fixtures;

namespace GhumoOdisha.Tests;

/// <summary>The organizer gets a WhatsApp alert (OTP template) with the number of every new customer.</summary>
public class NewCustomerAlertTests
{
    [Fact]
    public async Task WhatsAppSignUp_AlertsOrganizer_WithTheCustomersNumber()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();

        await service.RequestOtpAsync(new RequestOtpRequest("Priya Nayak", phone));
        await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        Assert.Equal(1, fake.TemplateCallCount);
        Assert.Equal(TestServices.AdminWhatsAppNumber, fake.LastPhoneNumber);
        Assert.Equal("New Customer", fake.LastTemplateVariable1);
        Assert.Contains("Priya Nayak", fake.LastTemplateVariable2);
        Assert.Contains($"+91 {phone[..5]} {phone[5..]}", fake.LastTemplateVariable2);
    }

    [Fact]
    public async Task ReturningCustomer_SigningInAgain_SendsNoAlert()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();
        var now = DateTime.UtcNow;
        db.Customers.Add(new Customer { Name = "Priya", PhoneNumber = phone, IsVerified = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        await service.RequestOtpAsync(new RequestOtpRequest(null, phone));
        await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        Assert.Equal(0, fake.TemplateCallCount);
    }

    [Fact]
    public async Task CoTravellerAccount_FirstOwnSignIn_AlertsOrganizer()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeWhatsAppService();
        var service = TestServices.CreateCustomerAuthService(db, fake);
        var phone = TestDb.RandomPhoneNumber();
        var now = DateTime.UtcNow;
        // Created unverified when someone else listed this number as a co-traveller.
        db.Customers.Add(new Customer { Name = "Co Traveller", PhoneNumber = phone, IsVerified = false, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        await service.RequestOtpAsync(new RequestOtpRequest(null, phone));
        await service.VerifyOtpAsync(new VerifyOtpRequest(phone, fake.LastOtp!));

        Assert.Equal(1, fake.TemplateCallCount);
        Assert.Contains("Co Traveller", fake.LastTemplateVariable2);
    }

    [Fact]
    public async Task GoogleSignUp_AlertsOrganizer_WithEmailWhenThereIsNoNumber()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeWhatsAppService();
        var google = new FakeGoogleTokenValidator();
        var service = TestServices.CreateCustomerAuthService(db, fake, fakeGoogle: google);
        var email = $"new{Guid.NewGuid():N}@example.com";

        await service.GoogleSignInAsync(new GoogleSignInRequest(google.Issue(email: email, name: "Asha")));

        Assert.Equal(1, fake.TemplateCallCount);
        Assert.Equal(TestServices.AdminWhatsAppNumber, fake.LastPhoneNumber);
        Assert.Contains("via Google", fake.LastTemplateVariable2);
        Assert.Contains(email, fake.LastTemplateVariable2);
    }

    [Fact]
    public async Task FailedAlert_DoesNotBlockSignIn()
    {
        await using var db = TestDb.CreateContext();
        var fake = new FakeWhatsAppService();
        var google = new FakeGoogleTokenValidator();
        var service = TestServices.CreateCustomerAuthService(db, fake, fakeGoogle: google);
        fake.ShouldFail = true;

        var auth = await service.GoogleSignInAsync(new GoogleSignInRequest(google.Issue(email: $"f{Guid.NewGuid():N}@example.com")));

        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public void Message_IsOneLine_WithNameAndFormattedNumber()
    {
        var text = NewCustomerNotifier.Message(new Customer { Name = " Rahul ", PhoneNumber = "9876543210" }, "WhatsApp");

        Assert.Equal("New customer signed up via WhatsApp: Rahul, WhatsApp: +91 98765 43210", text);
        Assert.DoesNotContain('\n', text);
    }
}
