namespace GhumoOdisha.Application.Contact;

/// <param name="OfficeAddress">Company:Address, shown in the footer.</param>
/// <param name="OfficeMapUrl">Directions link for the office (Company:MapUrl, or a Maps search for the address).</param>
/// <param name="OfficePhotoUrl">Admin-uploaded office photo, if any.</param>
public record ContactDto(string Name, string Role, string Phone, string WhatsAppNumber, string Email, string? PhotoUrl, string? InstagramUrl,
    string? OfficeAddress, string? OfficeMapUrl, string? OfficePhotoUrl);
