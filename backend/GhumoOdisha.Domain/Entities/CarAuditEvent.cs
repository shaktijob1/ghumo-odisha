using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// History for the Cars module: approvals, pricing changes, booking status, trip start/end and fare
/// corrections — who did it, when, and the value before and after. Events on a booking double as the
/// customer's tracking timeline (those with <see cref="IsVisibleToCustomer"/>).
/// </summary>
public class CarAuditEvent
{
    public long CarAuditEventId { get; set; }
    public CarAuditEntity EntityType { get; set; }
    public int EntityId { get; set; }
    /// <summary>Set for events about a booking or its trip, so a booking's timeline is one indexed query.</summary>
    public int? CarBookingId { get; set; }
    /// <summary>Machine-readable, e.g. "DriverApproved", "PricingSubmitted", "TripStarted".</summary>
    public string Action { get; set; } = null!;
    /// <summary>Readable line for timelines, e.g. "Trip started".</summary>
    public string Title { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Note { get; set; }
    /// <summary>"Customer", "Driver", "Admin" or "System".</summary>
    public string ActorRole { get; set; } = null!;
    public int? ActorId { get; set; }
    public bool IsVisibleToCustomer { get; set; }
    public DateTime CreatedAt { get; set; }
}
