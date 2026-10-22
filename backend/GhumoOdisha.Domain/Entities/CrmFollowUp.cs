using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A scheduled follow-up with a CRM customer, optionally about one trip interest. The "next
/// follow-up date" shown for a customer or interest is its earliest pending follow-up.
/// </summary>
public class CrmFollowUp
{
    public int CrmFollowUpId { get; set; }
    public int CustomerId { get; set; }
    public int? CustomerTripInterestId { get; set; }
    public DateOnly DueDate { get; set; }
    public string? Note { get; set; }
    public CrmFollowUpStatus Status { get; set; }
    public int? AssignedAdminUserId { get; set; }
    public int? CreatedByAdminUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByAdminUserId { get; set; }
    public string? Outcome { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public CustomerTripInterest? TripInterest { get; set; }
    public AdminUser? AssignedAdminUser { get; set; }
}
