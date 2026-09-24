namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Sends the approved Fast2SMS WhatsApp template message. The template is fixed and takes exactly
/// two variables — there is no per-message-type template, so every send (OTP, booking notices)
/// reuses this same one. Implementations must never log the OTP or the API key.
/// </summary>
public interface IFast2SmsWhatsAppService
{
    Task SendOtpAsync(string phoneNumber, string customerName, string otp, CancellationToken cancellationToken = default);

    /// <summary>Sends the same two-variable template with arbitrary content — used for booking
    /// request/confirmation notices, which have no dedicated approved template of their own.</summary>
    Task SendTemplateAsync(string phoneNumber, string variable1, string variable2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the dedicated "Booking Confirmed" template. Returns false without sending when that
    /// template isn't configured (Fast2Sms:BookingConfirmedMessageId empty), so the caller can fall
    /// back to <see cref="SendTemplateAsync"/>. Throws WhatsAppDeliveryException if the send fails.
    /// </summary>
    Task<bool> SendBookingConfirmedAsync(string phoneNumber, BookingConfirmedWhatsAppMessage message, CancellationToken cancellationToken = default);
}
