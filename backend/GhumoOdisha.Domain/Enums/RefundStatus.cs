namespace GhumoOdisha.Domain.Enums;

/// <summary>Lifecycle of a <see cref="Entities.BookingRefund"/>. Refunds are never automatic.</summary>
public enum RefundStatus
{
    /// <summary>Booking cancelled — waiting for the admin to issue the refund.</summary>
    Pending = 0,
    /// <summary>Admin issued it (Razorpay refund created, or a manual transfer recorded) — money on its way.</summary>
    Processing = 1,
    /// <summary>Admin confirmed the money reached the customer.</summary>
    Settled = 2
}
