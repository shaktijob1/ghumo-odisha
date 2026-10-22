namespace GhumoOdisha.Domain.Enums;

/// <summary>
/// What the CRM knows about a logged message. WhatsApp messages are sent by hand from WhatsApp Web
/// (no WhatsApp API), so the CRM never knows whether they were delivered or read — only that the
/// admin opened WhatsApp, and whether they later confirmed they pressed Send.
/// </summary>
public enum CrmMessageStatus
{
    /// <summary>The admin opened WhatsApp with the prefilled message; sending isn't confirmed yet.</summary>
    WhatsAppOpened = 0,
    /// <summary>The admin confirmed they pressed Send in WhatsApp.</summary>
    SentManually = 1,
    /// <summary>The admin said the message was not sent after all.</summary>
    NotConfirmed = 2,
    /// <summary>A call, email or other contact the admin recorded by hand.</summary>
    Logged = 3
}
