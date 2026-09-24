namespace GhumoOdisha.Domain.Entities;

/// <summary>One row per customer's use of a coupon — the unique index on (CouponCodeId, CustomerId)
/// is what actually enforces "once per customer," not application logic alone.</summary>
public class CouponRedemption
{
    public int CouponRedemptionId { get; set; }
    public int CouponCodeId { get; set; }
    public int CustomerId { get; set; }
    public int BookingId { get; set; }
    public decimal DiscountAmount { get; set; }

    // Snapshotted at redemption time so a later change to the coupon's commission rate, or the
    // customer's later bookings, never rewrite what the coupon holder earned on this booking.
    public int NumberOfSeats { get; set; }
    public bool IsNewCustomer { get; set; }
    public decimal CommissionAmount { get; set; }
    public DateTime RedeemedAt { get; set; }

    public CouponCode CouponCode { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
}
