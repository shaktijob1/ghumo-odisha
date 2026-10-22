namespace GhumoOdisha.Application.Contact;

/// <param name="OfficeAddress">Company:Address, shown in the footer.</param>
/// <param name="OfficeMapUrl">Directions link for the office (Company:MapUrl, or a Maps search for the address).</param>
/// <param name="OfficePhotoUrl">Admin-uploaded office photo, if any.</param>
/// <param name="Quote">Travel Organizer card: the organizer's line in quotes.</param>
/// <param name="PhotoCaption">Travel Organizer card: caption over the photo.</param>
/// <param name="HelpTitle">Travel Organizer card: bold line above the buttons.</param>
/// <param name="HelpText">Travel Organizer card: line under <paramref name="HelpTitle"/>.</param>
public record ContactDto(string Name, string Role, string Phone, string WhatsAppNumber, string Email, string? PhotoUrl, string? InstagramUrl,
    string? OfficeAddress, string? OfficeMapUrl, string? OfficePhotoUrl,
    string Quote, string PhotoCaption, string HelpTitle, string HelpText);
