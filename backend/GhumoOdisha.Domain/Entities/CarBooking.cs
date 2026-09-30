using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A customer's rental of one car for a time window. The car's owner-driver is assigned automatically.
/// Keeps the estimate (quoted at booking) and the final fare (after the trip) side by side — the
/// estimate is never overwritten. All amounts are computed server-side from <see cref="CarPricing"/>.
/// </summary>
public class CarBooking
{
    public int CarBookingId { get; set; }
    /// <summary>Random customer-facing number, shown as "GC-123456". Unique.</summary>
    public int BookingNumber { get; set; }
    public string Reference => $"GC-{BookingNumber}";
    public int CustomerId { get; set; }
    public int CarId { get; set; }
    public int DriverId { get; set; }
    /// <summary>The exact pricing version quoted; approved pricing rows never change.</summary>
    public int CarPricingId { get; set; }
    public Guid? ClientRequestId { get; set; }

    public string PickupCity { get; set; } = null!;
    /// <summary>The full address the customer typed (house, street, landmark) — what the driver navigates to.</summary>
    public string? PickupAddress { get; set; }
    /// <summary>The pickup as picked on the map (place name / "current location" address) and its point.</summary>
    public string? PickupLocation { get; set; }
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    /// <summary>The customer's "where to" (picked on the map).</summary>
    public string? DropLocation { get; set; }
    public double? DropLatitude { get; set; }
    public double? DropLongitude { get; set; }
    /// <summary>One way ends at "where to"; a round trip comes back to the pickup.</summary>
    public bool RoundTrip { get; set; }
    /// <summary>Road km: base → pickup, pickup → "where to", ("where to" → pickup on a round trip), back to base. Estimated km = their sum.</summary>
    public int? DriverApproachKm { get; set; }
    public int? PickupToDropKm { get; set; }
    public int? DropToPickupKm { get; set; }
    public int? ReturnToBaseKm { get; set; }
    public DateTime PickupAt { get; set; }
    public int DurationHours { get; set; }
    public DateTime EndsAt { get; set; }

    // --- Estimate (at booking) ---
    /// <summary>Billed km: driver → pickup + pickup → drop + drop → driver (older bookings: the km the customer typed).</summary>
    public int EstimatedKm { get; set; }
    public int EstimatedNights { get; set; }
    public decimal EstimatedBaseFare { get; set; }
    public decimal EstimatedKmCharge { get; set; }
    public decimal EstimatedNightHaltCharge { get; set; }
    public decimal EstimatedTotal { get; set; }

    /// <summary>Paid online to confirm (₹99 by default, from configuration).</summary>
    public decimal BookingAmount { get; set; }

    // --- Final (after the trip) ---
    public int? FinalKm { get; set; }
    public int? FinalNights { get; set; }
    public decimal? FinalBaseFare { get; set; }
    public decimal? FinalKmCharge { get; set; }
    public decimal? FinalNightHaltCharge { get; set; }
    public decimal? AdditionalCharges { get; set; }
    public string? AdditionalChargesNote { get; set; }
    public decimal? FinalTotal { get; set; }
    /// <summary>Final total − booking amount, paid to the driver after the trip.</summary>
    public decimal? BalanceDue { get; set; }
    public DateTime? BalanceCollectedAt { get; set; }

    public CarBookingStatus Status { get; set; }
    public CarPaymentStatus PaymentStatus { get; set; }
    /// <summary>An unpaid booking holds the car only until this time.</summary>
    public DateTime? HoldExpiresAt { get; set; }

    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public DateTime? PaidAt { get; set; }

    // --- Refund of the booking amount (manual queue, never automatic on cancel) ---
    public decimal? RefundAmount { get; set; }
    public PaymentMethod? RefundMethod { get; set; }
    public string? RefundReference { get; set; }
    public string? RazorpayRefundId { get; set; }
    public DateTime? RefundIssuedAt { get; set; }
    public DateTime? RefundSettledAt { get; set; }

    public string? CustomerNotes { get; set; }
    public string? AdminNotes { get; set; }
    /// <summary>"Customer", "Driver", "Admin" or "System".</summary>
    public string? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Car Car { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
    public CarPricing CarPricing { get; set; } = null!;
    public CarTripExecution? Execution { get; set; }
}
