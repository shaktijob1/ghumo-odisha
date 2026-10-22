namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Single-row table: the home page's "Travel Organizer" card — photo plus the texts the admin can
/// edit. Every text left null falls back to appsettings OrganizerContact / the built-in wording.
/// </summary>
public class OrganizerPhoto
{
    public int OrganizerPhotoId { get; set; }
    public string? ImageUrl { get; set; }
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? InstagramUrl { get; set; }
    public string? Quote { get; set; }
    public string? PhotoCaption { get; set; }
    public string? HelpTitle { get; set; }
    public string? HelpText { get; set; }
    public DateTime UpdatedAt { get; set; }
}
