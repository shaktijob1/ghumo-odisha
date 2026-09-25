namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Audit trail of every change an admin makes (confirm, cancel, record payment, edit trip, payout …),
/// recorded automatically for all non-GET admin API calls — who, what, on which record, and whether it worked.
/// </summary>
public class AdminActivity
{
    public long AdminActivityId { get; set; }
    public int AdminUserId { get; set; }

    /// <summary>Readable action, e.g. "Add payment", "Change seats", "Delete trip photo".</summary>
    public string Action { get; set; } = null!;

    /// <summary>What was acted on, e.g. "Booking", "Trip", "Coupon".</summary>
    public string Area { get; set; } = null!;
    public int? TargetId { get; set; }
    public string HttpMethod { get; set; } = null!;
    public string Path { get; set; } = null!;
    public int StatusCode { get; set; }
    public bool Succeeded { get; set; }

    /// <summary>Links to the request's logs on the Logs screen.</summary>
    public string? RequestId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
