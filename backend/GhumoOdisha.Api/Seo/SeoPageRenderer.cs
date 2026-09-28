using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Seo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Seo;

/// <summary>
/// Serves the Angular shell (wwwroot/index.html) with each page's own title, description, canonical
/// link, WhatsApp/Facebook preview tags and schema.org structured data written into the &lt;head&gt;.
/// Crawlers and link-preview bots don't run JavaScript, so without this every page looks like an
/// empty, identical home page to them. Angular then boots as usual and keeps the tags up to date
/// while the visitor navigates.
/// </summary>
public partial class SeoPageRenderer(
    IWebHostEnvironment env,
    ISeoService seo,
    IMemoryCache cache,
    IOptions<SeoOptions> seoOptions,
    IOptions<CompanyOptions> companyOptions,
    IOptions<OrganizerContactOptions> contactOptions)
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);
    private static readonly JsonSerializerOptions JsonLdOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    // Customer pages that must never show up in search results (private or placeholder).
    private static readonly string[] NoIndexPrefixes = ["/admin", "/login", "/my-bookings", "/profile", "/cars", "/hotels"];

    private readonly SeoOptions _seo = seoOptions.Value;

    private sealed record SeoPage(
        string Title,
        string Description,
        string? CanonicalPath,
        string? Image,
        string OgType = "website",
        bool NoIndex = false,
        int StatusCode = StatusCodes.Status200OK,
        IReadOnlyList<object>? JsonLd = null);

    public async Task RenderAsync(HttpContext context)
    {
        var shell = LoadShell();
        if (shell is null)
        {
            // No Angular build in wwwroot (e.g. running the API alone during development).
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var page = await BuildPageAsync(context.Request.Path.Value ?? "/", context.RequestAborted);
        context.Response.StatusCode = page.StatusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync(Inject(shell, page), context.RequestAborted);
    }

    // ---------- Per-page content ----------

    private async Task<SeoPage> BuildPageAsync(string path, CancellationToken cancellationToken)
    {
        var normalized = path.Length > 1 ? path.TrimEnd('/') : path;
        var lower = normalized.ToLowerInvariant();

        if (lower == "/")
        {
            var home = await seo.GetHomeAsync(cancellationToken);
            return new SeoPage(_seo.DefaultTitle, _seo.DefaultDescription, "/", home.HeroImageUrl ?? _seo.DefaultImage,
                JsonLd: [WebSiteSchema(), AgencySchema(home.HeroImageUrl)]);
        }

        if (lower == "/trips")
        {
            var home = await seo.GetHomeAsync(cancellationToken);
            return new SeoPage(
                $"Odisha Tour Packages & Group Trips | {_seo.SiteName}",
                "Browse all Ghumo Odisha group trips and tour packages — Puri, Konark, Koraput, Chilika and more. Fixed departures, AC stay and travel, and a trip coordinator. Book your seat online.",
                "/trips", home.HeroImageUrl ?? _seo.DefaultImage,
                JsonLd: [ItemListSchema(home.Trips), BreadcrumbSchema(("Trips", "/trips"))]);
        }

        if (lower.StartsWith("/trips/", StringComparison.Ordinal))
        {
            var tripId = SeoSlug.ParseTripId(normalized["/trips/".Length..]);
            var trip = tripId is null ? null : await seo.GetTripAsync(tripId.Value, cancellationToken);
            return trip is null ? NotFound() : TripPage(trip);
        }

        if (lower.StartsWith("/destinations/", StringComparison.Ordinal))
        {
            var destination = await seo.GetDestinationAsync(normalized["/destinations/".Length..], cancellationToken);
            return destination is null ? NotFound() : DestinationPage(destination);
        }

        if (lower == "/terms")
        {
            return new SeoPage($"Trip Terms & Conditions | {_seo.SiteName}",
                "Booking, payment, cancellation and refund terms for Ghumo Odisha trips.", "/terms", _seo.DefaultImage);
        }

        if (NoIndexPrefixes.Any(p => lower == p || lower.StartsWith(p + "/", StringComparison.Ordinal)))
        {
            return new SeoPage(_seo.DefaultTitle, _seo.DefaultDescription, null, _seo.DefaultImage, NoIndex: true);
        }

        return NotFound();
    }

    private SeoPage TripPage(TripSeoInfo trip)
    {
        var price = Rupees(trip.AmountPerPerson);
        var duration = trip.DurationLabel is null ? "" : $" – {trip.DurationLabel}";
        var places = trip.DestinationNames.Count > 0 ? $" Covers {string.Join(", ", trip.DestinationNames)}." : "";
        var description = Truncate($"{trip.Title}{duration} group trip from {price} per person.{places} {Plain(trip.Description)}", 160);

        var offer = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["price"] = trip.AmountPerPerson.ToString("0.00", CultureInfo.InvariantCulture),
            ["priceCurrency"] = "INR",
            ["url"] = Absolute(trip.Path),
            ["availability"] = trip.UpcomingDepartures.Any(d => d.AvailableSeats > 0) ? "https://schema.org/InStock" : "https://schema.org/SoldOut",
            ["validFrom"] = trip.UpcomingDepartures.FirstOrDefault()?.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        var touristTrip = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "TouristTrip",
            ["name"] = trip.Title,
            ["description"] = Truncate(Plain(trip.Description), 500),
            ["url"] = Absolute(trip.Path),
            ["image"] = AbsoluteOrNull(trip.ImageUrl),
            ["touristType"] = "Group travellers",
            ["itinerary"] = trip.DestinationNames.Count == 0 ? null : new Dictionary<string, object?>
            {
                ["@type"] = "ItemList",
                ["itemListElement"] = trip.DestinationNames.Select((name, i) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = i + 1,
                    ["item"] = new Dictionary<string, object?> { ["@type"] = "TouristDestination", ["name"] = name },
                }).ToList(),
            },
            ["offers"] = offer,
            ["provider"] = new Dictionary<string, object?> { ["@type"] = "TravelAgency", ["name"] = _seo.SiteName, ["url"] = Absolute("/") },
        };

        return new SeoPage(
            $"{trip.Title}{duration} from {price} | {_seo.SiteName}",
            description,
            trip.Path,
            trip.ImageUrl ?? _seo.DefaultImage,
            JsonLd: [touristTrip, BreadcrumbSchema(("Trips", "/trips"), (trip.Title, trip.Path))]);
    }

    private SeoPage DestinationPage(DestinationSeoInfo d)
    {
        var path = $"/destinations/{d.Slug}";
        var cheapest = d.Trips.Count > 0 ? $" Trips from {Rupees(d.Trips.Min(t => t.AmountPerPerson))}." : "";
        var about = Plain(d.AboutText ?? d.Tagline ?? d.KnownFor ?? "");
        var description = Truncate($"Explore {d.Name}, Odisha with Ghumo Odisha group trips and tour packages.{cheapest} {about}", 160);

        var destination = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "TouristDestination",
            ["name"] = d.Name,
            ["description"] = Truncate(about, 500),
            ["url"] = Absolute(path),
            ["image"] = AbsoluteOrNull(d.ImageUrl),
            ["containedInPlace"] = new Dictionary<string, object?> { ["@type"] = "State", ["name"] = "Odisha" },
        };

        return new SeoPage(
            $"{d.Name} Tour Packages & Trips | {_seo.SiteName}",
            description,
            path,
            d.ImageUrl ?? _seo.DefaultImage,
            JsonLd: [destination, ItemListSchema(d.Trips), BreadcrumbSchema((d.Name, path))]);
    }

    private SeoPage NotFound() =>
        new($"Page not found | {_seo.SiteName}", _seo.DefaultDescription, null, _seo.DefaultImage, NoIndex: true, StatusCode: StatusCodes.Status404NotFound);

    // ---------- schema.org blocks ----------

    private Dictionary<string, object?> WebSiteSchema() => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "WebSite",
        ["name"] = _seo.SiteName,
        ["url"] = Absolute("/"),
    };

    private Dictionary<string, object?> AgencySchema(string? image)
    {
        var company = companyOptions.Value;
        var phone = string.IsNullOrWhiteSpace(company.Phone) ? contactOptions.Value.Phone : company.Phone;
        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "TravelAgency",
            ["name"] = _seo.SiteName,
            ["url"] = Absolute("/"),
            ["logo"] = Absolute("/favicon.png"),
            ["image"] = AbsoluteOrNull(image ?? _seo.DefaultImage),
            ["description"] = _seo.DefaultDescription,
            ["telephone"] = string.IsNullOrWhiteSpace(phone) ? null : phone,
            ["email"] = string.IsNullOrWhiteSpace(company.Email) ? null : company.Email,
            ["address"] = string.IsNullOrWhiteSpace(_seo.AddressLocality) ? null : new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = _seo.StreetAddress,
                ["addressLocality"] = _seo.AddressLocality,
                ["addressRegion"] = _seo.AddressRegion,
                ["postalCode"] = _seo.PostalCode,
                ["addressCountry"] = "IN",
            },
            ["areaServed"] = new Dictionary<string, object?> { ["@type"] = "State", ["name"] = "Odisha" },
            ["sameAs"] = _seo.SameAs.Count > 0 ? _seo.SameAs : null,
        };
    }

    private Dictionary<string, object?> ItemListSchema(IReadOnlyList<DestinationTripLink> trips) => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "ItemList",
        ["itemListElement"] = trips.Select((t, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["url"] = Absolute(t.Path),
            ["name"] = t.Title,
        }).ToList(),
    };

    private Dictionary<string, object?> BreadcrumbSchema(params (string Name, string Path)[] trail) => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = new[] { ("Home", "/") }.Concat(trail).Select((c, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = c.Item1,
            ["item"] = Absolute(c.Item2),
        }).ToList(),
    };

    // ---------- HTML ----------

    private string Inject(string shell, SeoPage page)
    {
        var image = AbsoluteOrNull(page.Image);
        var head = new StringBuilder();
        head.Append("<title>").Append(Html.Encode(page.Title)).Append("</title>\n");
        Meta(head, "name", "description", page.Description);
        if (page.NoIndex) Meta(head, "name", "robots", "noindex");
        if (page.CanonicalPath is not null) head.Append($"  <link rel=\"canonical\" href=\"{Html.Encode(Absolute(page.CanonicalPath))}\">\n");

        Meta(head, "property", "og:site_name", _seo.SiteName);
        Meta(head, "property", "og:type", page.OgType);
        Meta(head, "property", "og:locale", "en_IN");
        Meta(head, "property", "og:title", page.Title);
        Meta(head, "property", "og:description", page.Description);
        if (page.CanonicalPath is not null) Meta(head, "property", "og:url", Absolute(page.CanonicalPath));
        if (image is not null)
        {
            Meta(head, "property", "og:image", image);
            Meta(head, "property", "og:image:alt", page.Title);
        }
        Meta(head, "name", "twitter:card", image is null ? "summary" : "summary_large_image");
        Meta(head, "name", "twitter:title", page.Title);
        Meta(head, "name", "twitter:description", page.Description);
        if (image is not null) Meta(head, "name", "twitter:image", image);

        foreach (var block in page.JsonLd ?? [])
        {
            // The default encoder escapes <, > and & so the JSON can never close the script tag.
            head.Append("  <script type=\"application/ld+json\">").Append(JsonSerializer.Serialize(block, JsonLdOptions)).Append("</script>\n");
        }

        var cleaned = TitleTag().Replace(shell, "", 1);
        cleaned = DescriptionTag().Replace(cleaned, "", 1);
        var headEnd = cleaned.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return headEnd < 0 ? cleaned : cleaned.Insert(headEnd, head.ToString());
    }

    private static void Meta(StringBuilder head, string attr, string key, string value) =>
        head.Append($"  <meta {attr}=\"{key}\" content=\"{Html.Encode(value)}\">\n");

    private string? LoadShell()
    {
        var file = env.WebRootFileProvider.GetFileInfo("index.html");
        if (!file.Exists || file.PhysicalPath is null)
        {
            return null;
        }

        // Re-read only when a new Angular build replaces the file.
        return cache.GetOrCreate($"seo:shell:{file.LastModified.UtcTicks}", entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(6);
            return File.ReadAllText(file.PhysicalPath);
        });
    }

    // ---------- helpers ----------

    private string Absolute(string path) =>
        path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? path
            : _seo.SiteUrl.TrimEnd('/') + "/" + path.TrimStart('/');

    private string? AbsoluteOrNull(string? path) => string.IsNullOrWhiteSpace(path) ? null : Absolute(path);

    private static string Rupees(decimal amount) => "₹" + amount.ToString("#,##0.##", India);

    private static string Plain(string text) => Whitespace().Replace(text, " ").Trim();

    private static string Truncate(string text, int max)
    {
        text = Plain(text);
        if (text.Length <= max) return text;
        var cut = text.LastIndexOf(' ', max - 1);
        return text[..(cut > max / 2 ? cut : max - 1)].TrimEnd(',', '.', ';', ':', '-', '–') + "…";
    }

    [GeneratedRegex(@"<title>.*?</title>\s*", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();

    [GeneratedRegex(@"<meta\s+name=""description""[^>]*>\s*", RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
