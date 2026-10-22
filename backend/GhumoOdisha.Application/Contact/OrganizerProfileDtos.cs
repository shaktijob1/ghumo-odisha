namespace GhumoOdisha.Application.Contact;

/// <summary>
/// Admin: the Travel Organizer card's editable texts as stored (null = not set, the site uses
/// <see cref="Defaults"/> instead). The WhatsApp number stays in appsettings OrganizerContact —
/// it's tied to the WhatsApp Business templates and booking alerts.
/// </summary>
public record OrganizerProfileDto(
    string? PhotoUrl,
    string? Name,
    string? Role,
    string? Phone,
    string? Email,
    string? InstagramUrl,
    string? Quote,
    string? PhotoCaption,
    string? HelpTitle,
    string? HelpText,
    OrganizerProfileDefaults Defaults);

/// <summary>What each field shows when left empty (appsettings / built-in wording).</summary>
public record OrganizerProfileDefaults(string Name, string Role, string Phone, string Email, string? InstagramUrl,
    string Quote, string PhotoCaption, string HelpTitle, string HelpText, string WhatsAppNumber);

public record UpdateOrganizerProfileRequest(
    string? Name,
    string? Role,
    string? Phone,
    string? Email,
    string? InstagramUrl,
    string? Quote,
    string? PhotoCaption,
    string? HelpTitle,
    string? HelpText);

/// <summary>The card exactly as the website shows it: stored value, else the default.</summary>
public record OrganizerCard(string Name, string Role, string Phone, string Email, string? InstagramUrl,
    string Quote, string PhotoCaption, string HelpTitle, string HelpText, string? PhotoUrl);
