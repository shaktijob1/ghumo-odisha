using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A car (or Tempo Traveller) registered by its owner-driver. Shown in customer search only while
/// the car is Approved, its driver is Approved and it has an approved active pricing.
/// </summary>
public class Car
{
    public int CarId { get; set; }
    public int DriverId { get; set; }
    public string Brand { get; set; } = null!;
    public string ModelName { get; set; } = null!;
    public string RegistrationNumber { get; set; } = null!;
    public FuelType FuelType { get; set; }
    public int SeatCapacity { get; set; }
    public bool HasAc { get; set; }
    public string? Description { get; set; }
    /// <summary>City the car is based in — the pickup location customers search by.</summary>
    public string BaseCity { get; set; } = null!;

    public CarStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public DateTime? SubmittedForReviewAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }

    /// <summary>The approved pricing customers are charged. Null until the first pricing is approved.</summary>
    public int? ActivePricingId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Driver Driver { get; set; } = null!;
    public CarPricing? ActivePricing { get; set; }
    public ICollection<CarPricing> Pricings { get; set; } = new List<CarPricing>();
    public ICollection<CarPhoto> Photos { get; set; } = new List<CarPhoto>();
    public ICollection<CarBooking> Bookings { get; set; } = new List<CarBooking>();
}
