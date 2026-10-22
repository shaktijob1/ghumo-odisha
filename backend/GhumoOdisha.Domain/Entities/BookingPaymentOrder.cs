namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Every Razorpay order created for a booking, with exactly what it quoted. A booking's
/// <see cref="Booking.RazorpayOrderId"/> only points at the newest order, so without this a payment
/// made on an earlier order (the page was refreshed, the plan or coupon changed) would be invisible —
/// money taken, booking never confirmed. The payment reconciler checks each of these with Razorpay.
/// </summary>
public class BookingPaymentOrder
{
    public int BookingPaymentOrderId { get; set; }
    public int BookingId { get; set; }
    public string RazorpayOrderId { get; set; } = null!;

    /// <summary>What Razorpay was asked to charge — a payment for any other amount is never accepted.</summary>
    public long AmountPaise { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public int? CouponCodeId { get; set; }
    public decimal? CouponDiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>The Razorpay payment that confirmed the booking from this order.</summary>
    public string? ConfirmedPaymentId { get; set; }

    /// <summary>
    /// A payment on this order that couldn't be used (the checkout had closed, or the booking was
    /// already paid on another order) and was refunded automatically — so it's never refunded twice.
    /// </summary>
    public string? RefundedPaymentId { get; set; }
    public string? RazorpayRefundId { get; set; }

    /// <summary>Last time the reconciler asked Razorpay about this order.</summary>
    public DateTime? LastCheckedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
