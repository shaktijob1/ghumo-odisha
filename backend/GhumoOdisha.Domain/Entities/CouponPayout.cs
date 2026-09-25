using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Commission paid out to a coupon holder, recorded by the admin after paying them manually
/// (UPI, bank transfer, cash, or a manual Razorpay transfer). Balance due to the holder is
/// commission earned on active bookings minus the sum of these rows — so a booking cancelled
/// after a payout can push it negative, which is then netted against future commission.
/// </summary>
public class CouponPayout
{
    public int CouponPayoutId { get; set; }
    public int CouponCodeId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public CouponCode CouponCode { get; set; } = null!;
}
