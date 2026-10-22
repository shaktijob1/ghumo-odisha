namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A custom holiday package listed in the home page's Holidays tab. There's no online booking for
/// holidays: customers enquire on WhatsApp and the organizer plans the rest.
/// </summary>
public class HolidayPackage
{
    public int HolidayPackageId { get; set; }
    public string Title { get; set; } = null!;
    public string? Summary { get; set; }
    public int Days { get; set; }
    public int Nights { get; set; }

    /// <summary>"From ₹…" per person; null shows "Price on request".</summary>
    public decimal? PriceFrom { get; set; }

    /// <summary>What's included / the main sights, one per line.</summary>
    public string? Highlights { get; set; }
    public string? CoverImageUrl { get; set; }
    public int? HolidayDestinationId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public HolidayDestination? HolidayDestination { get; set; }
}
