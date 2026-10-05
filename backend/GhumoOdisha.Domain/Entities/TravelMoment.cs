namespace GhumoOdisha.Domain.Entities;

/// <summary>One photo in the home page's "Real Travel Moments" gallery (admin-uploaded, at most 10).</summary>
public class TravelMoment
{
    public int TravelMomentId { get; set; }
    public string ImageUrl { get; set; } = null!;

    /// <summary>Short line shown on the photo and used as its alt text, e.g. "Sunrise at Deomali".</summary>
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
