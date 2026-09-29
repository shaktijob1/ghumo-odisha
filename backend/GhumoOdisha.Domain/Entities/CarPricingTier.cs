namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Base fare for trips up to <see cref="UpToKm"/> km. Tiers are ordered by UpToKm; the last
/// one has UpToKm = null and covers everything above the previous tier (e.g. "200+ km").
/// </summary>
public class CarPricingTier
{
    public int CarPricingTierId { get; set; }
    public int CarPricingId { get; set; }
    public int? UpToKm { get; set; }
    public decimal BaseFare { get; set; }

    public CarPricing CarPricing { get; set; } = null!;
}
