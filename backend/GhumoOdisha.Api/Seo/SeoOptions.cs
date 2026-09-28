namespace GhumoOdisha.Api.Seo;

/// <summary>
/// Site-wide search/link-preview settings. <see cref="SiteUrl"/> is the public address used in
/// canonical links, the sitemap and preview tags — always the live domain, even when running locally,
/// so a page can never tell Google its "real" address is localhost.
/// </summary>
public class SeoOptions
{
    public const string SectionName = "Seo";

    public string SiteUrl { get; set; } = "https://ghumoodisha.com";
    public string SiteName { get; set; } = "Ghumo Odisha";
    public string DefaultTitle { get; set; } = "Ghumo Odisha | Odisha Trips, Tours & Travel Packages";
    public string DefaultDescription { get; set; } = string.Empty;

    /// <summary>Preview image used when a page has none of its own (site path like "/uploads/hero/x.jpg" or a full URL).</summary>
    public string? DefaultImage { get; set; }

    // Structured business address for Google (schema.org PostalAddress).
    public string StreetAddress { get; set; } = string.Empty;
    public string AddressLocality { get; set; } = string.Empty;
    public string AddressRegion { get; set; } = "Odisha";
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Official social profiles (Instagram, Facebook, YouTube…) — tells Google they belong to this business.</summary>
    public List<string> SameAs { get; set; } = [];
}
