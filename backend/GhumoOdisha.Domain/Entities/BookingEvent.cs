using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>One step in a booking's history — the order-tracking timeline shown to admin and customer.</summary>
public class BookingEvent
{
    public int BookingEventId { get; set; }
    public int BookingId { get; set; }
    public BookingEventType EventType { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>"Customer", "Admin" or "System".</summary>
    public string Actor { get; set; } = null!;

    /// <summary>Internal steps (e.g. a corrected payment entry) are shown to the admin only.</summary>
    public bool IsVisibleToCustomer { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
