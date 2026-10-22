namespace GhumoOdisha.Domain.Entities;

/// <summary>A dated note on a CRM customer ("Asked about pickup from Patia"), optionally about one trip interest.</summary>
public class CrmNote
{
    public int CrmNoteId { get; set; }
    public int CustomerId { get; set; }
    public int? CustomerTripInterestId { get; set; }
    public string Text { get; set; } = null!;
    public int? AdminUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public CustomerTripInterest? TripInterest { get; set; }
    public AdminUser? AdminUser { get; set; }
}
