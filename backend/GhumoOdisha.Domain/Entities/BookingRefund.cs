using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// The refund owed on a cancelled booking. Created (Pending) when a paid booking is cancelled;
/// the admin then issues it — online through Razorpay or manually (UPI/bank/cash) — and marks it
/// settled once the money has reached the customer. At most one per booking.
/// </summary>
public class BookingRefund
{
    public int BookingRefundId { get; set; }
    public int BookingId { get; set; }

    /// <summary>What the customer had paid when the booking was cancelled (snapshot).</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>Amount being refunded — AmountPaid by default, the admin may refund less when issuing.</summary>
    public decimal Amount { get; set; }

    public RefundStatus Status { get; set; }

    /// <summary>How it was refunded — set when issued.</summary>
    public PaymentMethod? Method { get; set; }

    /// <summary>Razorpay refund id, or the UPI/bank UTR / receipt for a manual refund.</summary>
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>"Customer" or "Admin" — who cancelled the booking.</summary>
    public string RequestedBy { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? InitiatedAt { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
