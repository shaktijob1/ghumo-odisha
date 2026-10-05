namespace GhumoOdisha.Domain.Enums;

public enum BookingStatus
{
    /// <summary>
    /// "Requested": a website checkout that hasn't been paid yet. Hidden from the customer; listed for
    /// the admin (read-only, no seats held) until it's paid or the unpaid-request expiry cancels it.
    /// </summary>
    AwaitingPayment = 0,
    /// <summary>Legacy (no longer created) — treated exactly like <see cref="AwaitingPayment"/>.</summary>
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5
}
