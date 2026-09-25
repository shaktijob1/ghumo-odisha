using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>One row per customer page (<see cref="SiteHeroPage"/>) - that page's hero banner photo,
/// set independently of any trip's own photos. The frontend falls back when a page has none: the
/// Trips page to the Home photo, and Home to cycling trip cover images.</summary>
public class SiteHeroPhoto
{
    public int SiteHeroPhotoId { get; set; }
    public SiteHeroPage Page { get; set; }
    public string ImageUrl { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
