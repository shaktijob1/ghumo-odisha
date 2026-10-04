namespace GhumoOdisha.Application.Company;

/// <summary>The legal/business identity printed on customer-facing documents like invoices —
/// distinct from <c>OrganizerContactOptions</c>, which is the point-of-contact for WhatsApp/calls.</summary>
public class CompanyOptions
{
    public const string SectionName = "Company";

    public string Name { get; set; } = "Ghumo Odisha";
    public string Address { get; set; } = string.Empty;

    /// <summary>Google Maps link to the office ("Share" → copy link). Blank = a Maps search for <see cref="Address"/>.</summary>
    public string MapUrl { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>The address for display: "B55, DreamVilla, Bhubaneswar, 751024" (a space after every comma).</summary>
    public string? DisplayAddress() =>
        string.IsNullOrWhiteSpace(Address) ? null : System.Text.RegularExpressions.Regex.Replace(Address.Trim(), @",\s*", ", ");

    /// <summary>Directions link for the office: <see cref="MapUrl"/>, else a Google Maps search for the name and address.</summary>
    public string? DirectionsUrl() =>
        !string.IsNullOrWhiteSpace(MapUrl) ? MapUrl.Trim()
        : DisplayAddress() is { } address ? "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString($"{Name}, {address}")
        : null;

    /// <summary>GST registration number, if any. Left blank prints no GSTIN line on the invoice.</summary>
    public string Gstin { get; set; } = string.Empty;
}
