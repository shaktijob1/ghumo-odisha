namespace GhumoOdisha.Domain.Entities;

/// <summary>Single-row table — the organizer's UPI payment QR image, shown full-screen on the admin
/// Collections page so a coordinator can hold the phone up for a traveller to scan and pay the balance.</summary>
public class PaymentQrCode
{
    public int PaymentQrCodeId { get; set; }
    public string ImageUrl { get; set; } = null!;
    /// <summary>Optional caption under the QR, e.g. the UPI id or payee name.</summary>
    public string? Caption { get; set; }
    public DateTime UpdatedAt { get; set; }
}
