namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Sends approved WhatsApp template messages. The OTP template takes exactly two variables
/// ("Hello {{1}}, This is your {{2}} ...") and has no per-message-type siblings, so booking
/// notices without a dedicated template reuse it. Implementations must never log the OTP or
/// the WhatsApp access token.
/// </summary>
public interface IWhatsAppService
{
    Task SendOtpAsync(string phoneNumber, string customerName, string otp, CancellationToken cancellationToken = default);

    /// <summary>Sends the same two-variable template with arbitrary content — used for booking
    /// request/confirmation notices, which have no dedicated approved template of their own.</summary>
    Task SendTemplateAsync(string phoneNumber, string variable1, string variable2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the dedicated "Booking Confirmed" template. Returns false without sending when that
    /// template isn't configured (WhatsApp:BookingConfirmedTemplateName empty), so the caller can fall
    /// back to <see cref="SendTemplateAsync"/>. Throws WhatsAppDeliveryException if the send fails.
    /// </summary>
    Task<bool> SendBookingConfirmedAsync(string phoneNumber, BookingConfirmedWhatsAppMessage message, CancellationToken cancellationToken = default);
}
