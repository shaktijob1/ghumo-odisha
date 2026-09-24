namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Proof that a customer accepted the Terms &amp; Conditions for a specific booking. Text is a
/// verbatim snapshot of what they agreed to at that moment — never re-read from the live terms
/// content later, so a subsequent wording change can never retroactively alter this record.
/// </summary>
public class TermsAcceptance
{
    public int TermsAcceptanceId { get; set; }
    public int CustomerId { get; set; }
    public int BookingId { get; set; }
    public string Version { get; set; } = null!;
    public string Text { get; set; } = null!;
    public DateTime AcceptedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
}
