namespace GhumoOdisha.Application.Company;

/// <summary>The legal/business identity printed on customer-facing documents like invoices —
/// distinct from <c>OrganizerContactOptions</c>, which is the point-of-contact for WhatsApp/calls.</summary>
public class CompanyOptions
{
    public const string SectionName = "Company";

    public string Name { get; set; } = "Ghumo Odisha";
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>GST registration number, if any. Left blank prints no GSTIN line on the invoice.</summary>
    public string Gstin { get; set; } = string.Empty;
}
