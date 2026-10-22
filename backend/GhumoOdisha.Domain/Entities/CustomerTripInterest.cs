using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// One trip a CRM customer is interested in — a customer can have many ("Koraput + Araku, 14 Nov,
/// Hot Lead" and "Vizag, 28 Nov, Interested"). The departure is optional: an enquiry may name only
/// the trip until a date is agreed.
/// </summary>
public class CustomerTripInterest
{
    public int CustomerTripInterestId { get; set; }
    public int CustomerId { get; set; }
    public int TripId { get; set; }
    public int? TripDateSlotId { get; set; }
    public int Travellers { get; set; }
    public CrmStatus Status { get; set; }
    public DateTime InterestedAt { get; set; }
    public DateTime? LastContactAt { get; set; }
    public string? Notes { get; set; }
    /// <summary>The admin's estimate of what this interest is worth, for the pipeline view.</summary>
    public decimal? ExpectedAmount { get; set; }
    /// <summary>The real booking this interest turned into (website or admin booking). Unique.</summary>
    public int? BookingId { get; set; }
    public string? LostReason { get; set; }
    public int? CreatedByAdminUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
    public TripDateSlot? TripDateSlot { get; set; }
    public Booking? Booking { get; set; }
    public AdminUser? CreatedByAdminUser { get; set; }
}
