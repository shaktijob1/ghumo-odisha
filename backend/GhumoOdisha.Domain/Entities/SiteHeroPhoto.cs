namespace GhumoOdisha.Domain.Entities;

/// <summary>Single-row table - the customer dashboard's hero banner photo, set independently of any
/// trip's own photos. Falls back to cycling trip cover images on the frontend when this is unset.</summary>
public class SiteHeroPhoto
{
    public int SiteHeroPhotoId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}
