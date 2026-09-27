namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// One trip search made from the home page (month and/or place) — lets admins see what visitors
/// are looking for, including searches that found nothing.
/// </summary>
public class SearchLog
{
    public long SearchLogId { get; set; }

    /// <summary>Month searched, "yyyy-MM" — null when the visitor left it on "Any month".</summary>
    public string? Month { get; set; }

    /// <summary>Place searched — null when the visitor left it on "Any place".</summary>
    public string? Place { get; set; }

    /// <summary>How many upcoming trips the visitor was shown for this search.</summary>
    public int ResultCount { get; set; }

    /// <summary>Set when a signed-in customer searched; null for anonymous visitors.</summary>
    public int? CustomerId { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
