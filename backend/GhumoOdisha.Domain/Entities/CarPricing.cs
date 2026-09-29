using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// One version of a car's pricing. Immutable once reviewed: a change is always a new row (Pending),
/// so bookings keep pointing at the exact version they were quoted with and history is never lost.
/// Base fare comes from <see cref="Tiers"/> — by trip km — so it can fall as distance grows.
/// </summary>
public class CarPricing
{
    public int CarPricingId { get; set; }
    public int CarId { get; set; }
    public decimal PricePerKm { get; set; }
    public decimal NightHaltPrice { get; set; }
    public CarPricingStatus Status { get; set; }

    /// <summary>"Driver" or "Admin".</summary>
    public string SubmittedByRole { get; set; } = null!;
    public int SubmittedById { get; set; }
    public DateTime SubmittedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    /// <summary>Why it was rejected — shown to the driver.</summary>
    public string? ReviewNote { get; set; }

    public Car Car { get; set; } = null!;
    public ICollection<CarPricingTier> Tiers { get; set; } = new List<CarPricingTier>();
}
