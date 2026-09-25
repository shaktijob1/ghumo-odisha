using GhumoOdisha.Application.Auth;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>
/// Stands in for the real WhatsApp Cloud API call so the OTP flow can be exercised end-to-end in tests
/// without live credentials. Capturing the plaintext OTP here is test-only visibility — it
/// never touches the real API response contract, so it doesn't relax "never return the OTP."
/// </summary>
public class FakeWhatsAppService : IWhatsAppService
{
    public string? LastOtp { get; private set; }
    public string? LastPhoneNumber { get; private set; }
    public string? LastCustomerName { get; private set; }
    public int CallCount { get; private set; }
    public bool ShouldFail { get; set; }

    public string? LastTemplateVariable1 { get; private set; }
    public string? LastTemplateVariable2 { get; private set; }
    public int TemplateCallCount { get; private set; }

    /// <summary>Set to simulate WhatsApp:BookingConfirmedTemplateName being configured.</summary>
    public bool BookingConfirmedTemplateConfigured { get; set; }
    public BookingConfirmedWhatsAppMessage? LastBookingConfirmed { get; private set; }
    public int BookingConfirmedCallCount { get; private set; }

    public Task SendOtpAsync(string phoneNumber, string customerName, string otp, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new GhumoOdisha.Application.Exceptions.WhatsAppDeliveryException();
        }

        CallCount++;
        LastPhoneNumber = phoneNumber;
        LastCustomerName = customerName;
        LastOtp = otp;
        return Task.CompletedTask;
    }

    public Task SendTemplateAsync(string phoneNumber, string variable1, string variable2, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new GhumoOdisha.Application.Exceptions.WhatsAppDeliveryException();
        }

        TemplateCallCount++;
        LastPhoneNumber = phoneNumber;
        LastTemplateVariable1 = variable1;
        LastTemplateVariable2 = variable2;
        return Task.CompletedTask;
    }

    public Task<bool> SendBookingConfirmedAsync(string phoneNumber, BookingConfirmedWhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        if (!BookingConfirmedTemplateConfigured)
        {
            return Task.FromResult(false);
        }

        if (ShouldFail)
        {
            throw new GhumoOdisha.Application.Exceptions.WhatsAppDeliveryException();
        }

        BookingConfirmedCallCount++;
        LastPhoneNumber = phoneNumber;
        LastBookingConfirmed = message;
        return Task.FromResult(true);
    }
}
