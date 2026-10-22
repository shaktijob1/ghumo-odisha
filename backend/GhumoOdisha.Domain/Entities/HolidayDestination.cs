namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A place shown in the home page's Holidays tab ("Holiday destinations"). Separate from the trip
/// <see cref="Destination"/> list so holiday places never mix into group-trip pages, search or SEO.
/// </summary>
public class HolidayDestination
{
    public int HolidayDestinationId { get; set; }
    public string Name { get; set; } = null!;
    public string? Tagline { get; set; }
    public string? CoverImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<HolidayPackage> Packages { get; set; } = new List<HolidayPackage>();
}
