namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Meta WhatsApp Cloud API settings. Messages are sent from <see cref="PhoneNumberId"/> using
/// templates approved in that number's WhatsApp Business Account. <see cref="AccessToken"/> is a
/// secret — supply it via user-secrets locally or the WhatsApp__AccessToken environment variable,
/// never in a committed appsettings file.
/// </summary>
public class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string ApiBaseUrl { get; set; } = "https://graph.facebook.com";
    public string ApiVersion { get; set; } = "v25.0";

    /// <summary>The sending number's Phone number ID (WhatsApp Manager → API Setup), not the phone number itself.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>System-user access token with the whatsapp_business_messaging permission.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Approved OTP template. Empty = not set up yet: OTP sends fail with WhatsAppDeliveryException.
    /// Booking notices without a dedicated template also reuse it (see <see cref="IWhatsAppService.SendTemplateAsync"/>).
    /// </summary>
    public string OtpTemplateName { get; set; } = string.Empty;
    public string OtpTemplateLanguage { get; set; } = "en";

    /// <summary>
    /// Utility: body "Hello {{1}}, ... {{2}} ..." (name, code). Authentication: Meta's fixed
    /// "{{1}} is your verification code" body plus a copy-code button — it can't carry booking
    /// notices, so those are skipped when this is Authentication.
    /// </summary>
    public WhatsAppOtpTemplateCategory OtpTemplateCategory { get; set; } = WhatsAppOtpTemplateCategory.Utility;

    /// <summary>
    /// Approved "Booking Confirmed" template (9 variables — see <see cref="BookingConfirmedWhatsAppMessage"/>).
    /// Empty = not set up yet: confirmations fall back to the OTP template.
    /// </summary>
    public string BookingConfirmedTemplateName { get; set; } = string.Empty;
    public string BookingConfirmedTemplateLanguage { get; set; } = "en";
}

public enum WhatsAppOtpTemplateCategory
{
    Utility,
    Authentication
}
