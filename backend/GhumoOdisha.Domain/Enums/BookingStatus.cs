namespace GhumoOdisha.Domain.Enums;

public enum BookingStatus
{
    /// <summary>
    /// Internal only: a website checkout that hasn't been paid yet. Never shown to customers or in
    /// admin lists — a booking only exists for them once it's paid (Confirmed). Unpaid ones expire.
    /// </summary>
    AwaitingPayment = 0,
    /// <summary>Legacy (no longer created) — treated exactly like <see cref="AwaitingPayment"/>.</summary>
    Pending = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5
}
