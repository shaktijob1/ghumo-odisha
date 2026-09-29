namespace GhumoOdisha.Domain.Enums;

public enum CarBookingStatus
{
    /// <summary>Created, ₹ booking amount not paid yet. Holds the car only until HoldExpiresAt.</summary>
    PendingPayment = 0,
    /// <summary>Booking amount paid; the car's driver is assigned. Trip not started.</summary>
    Confirmed = 1,
    /// <summary>Driver pressed Start Trip.</summary>
    InProgress = 2,
    /// <summary>Driver completed the trip; final fare recorded.</summary>
    Completed = 3,
    Cancelled = 4,
    /// <summary>Never paid; the hold ran out.</summary>
    Expired = 5
}
