namespace GhumoOdisha.Domain.Enums;

public enum CarPaymentStatus
{
    Unpaid = 0,
    /// <summary>The online booking amount is paid; the balance is due to the driver after the trip.</summary>
    BookingAmountPaid = 1,
    /// <summary>Driver confirmed they collected the balance.</summary>
    BalanceCollected = 2,
    /// <summary>Cancelled after paying — waiting for the admin to refund the booking amount.</summary>
    RefundPending = 3,
    /// <summary>Admin issued the refund (Razorpay or manual) — money on its way.</summary>
    RefundProcessing = 4,
    Refunded = 5
}
