namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Fast2SMS is acting as the BSP in front of a Meta WhatsApp Business Account, but the
/// actual send call is Fast2SMS's own simplified trigger API: authorization + their
/// short numeric MessageId for the approved template + numbers + pipe-separated
/// variables_values. WabaId/PhoneNumberId/TemplateName aren't part of that call — kept
/// here only for reference/future use in case a different Fast2SMS endpoint needs them.
/// </summary>
public class Fast2SmsOptions
{
    public const string SectionName = "Fast2Sms";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://www.fast2sms.com/dev/whatsapp";

    /// <summary>Fast2SMS's own numeric id for the approved WhatsApp template (their "message_id" param).</summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Fast2SMS message_id of the approved "Booking Confirmed" template (9 variables — see
    /// <see cref="BookingConfirmedWhatsAppMessage"/>). Empty = not set up yet: confirmations fall
    /// back to the two-variable <see cref="MessageId"/> template.
    /// </summary>
    public string BookingConfirmedMessageId { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;
    public string WabaId { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
}
