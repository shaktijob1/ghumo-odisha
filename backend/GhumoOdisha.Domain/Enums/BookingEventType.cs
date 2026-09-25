namespace GhumoOdisha.Domain.Enums;

public enum BookingEventType
{
    Requested = 0,
    Confirmed = 1,
    Rejected = 2,
    Cancelled = 3,
    Completed = 4,
    PaymentReceived = 5,
    PaymentRemoved = 6,
    SeatsChanged = 7,
    TravellersUpdated = 8,
    GenderCountsUpdated = 9,
    Created = 10
}
