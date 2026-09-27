namespace GhumoOdisha.Domain.Enums;

public enum PaymentStatus
{
    Unpaid = 0,
    AdvancePaid = 1,
    Paid = 2,
    /// <summary>Money has actually been returned — the admin marked the refund settled.</summary>
    Refunded = 3,
    /// <summary>Cancelled with money paid; a refund request is waiting for / in the admin's hands.</summary>
    RefundPending = 4
}
