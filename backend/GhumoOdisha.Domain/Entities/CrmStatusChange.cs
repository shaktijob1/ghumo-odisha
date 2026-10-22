using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// History of CRM stage changes, for a customer or (when <see cref="CustomerTripInterestId"/> is set)
/// one of their trip interests. A null admin means the system did it (e.g. a website booking).
/// </summary>
public class CrmStatusChange
{
    public int CrmStatusChangeId { get; set; }
    public int CustomerId { get; set; }
    public int? CustomerTripInterestId { get; set; }
    public CrmStatus? FromStatus { get; set; }
    public CrmStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public int? AdminUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public CustomerTripInterest? TripInterest { get; set; }
    public AdminUser? AdminUser { get; set; }
}
