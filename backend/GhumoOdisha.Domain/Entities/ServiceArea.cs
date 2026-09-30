namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Where cars pick customers up from. A pickup is serviceable when it lies inside the drawn boundary of
/// any active area, or its PIN code is in any active area's list. While no area is active, every pickup
/// is accepted (the restriction starts with the first area the admin adds).
/// </summary>
public class ServiceArea
{
    public int ServiceAreaId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    /// <summary>Drawn zone: JSON array of [lat, lng] pairs (implicitly closed). Null when the area is PIN codes only.</summary>
    public string? BoundaryJson { get; set; }
    /// <summary>Comma-separated 6-digit PIN codes, normalised. Null when the area is a drawn zone only.</summary>
    public string? Pincodes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
