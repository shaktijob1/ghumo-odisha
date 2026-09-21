namespace GhumoOdisha.Domain.Entities;

/// <summary>Single-row table — there's exactly one organizer photo shown on the customer dashboard.</summary>
public class OrganizerPhoto
{
    public int OrganizerPhotoId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
