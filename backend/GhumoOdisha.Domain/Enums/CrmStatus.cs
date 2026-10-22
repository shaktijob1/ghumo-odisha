namespace GhumoOdisha.Domain.Enums;

/// <summary>
/// Sales stage of a CRM customer, and of each of their trip interests. The Kanban pipeline runs
/// NewLead → Interested → HotLead → BookingPending → Booked → Completed; the rest are side states.
/// <see cref="RepeatCustomer"/> only applies to a customer, not to a single trip interest.
/// </summary>
public enum CrmStatus
{
    NewLead = 0,
    Interested = 1,
    HotLead = 2,
    FollowUp = 3,
    BookingPending = 4,
    Booked = 5,
    Completed = 6,
    Cancelled = 7,
    NotInterested = 8,
    Later = 9,
    RepeatCustomer = 10
}
