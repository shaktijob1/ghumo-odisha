using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Destinations;
using GhumoOdisha.Application.Destinations.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Legal;
using GhumoOdisha.Application.Seo;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Seo;

/// <summary>Everything search engines and link previews need about one page.</summary>
/// <param name="Body">Server-rendered page content written inside &lt;app-root&gt; (null for private pages).</param>
public sealed record SeoPage(
    string Title,
    string Description,
    string? CanonicalPath,
    string? Image,
    string OgType = "website",
    bool NoIndex = false,
    int StatusCode = StatusCodes.Status200OK,
    IReadOnlyList<object>? JsonLd = null,
    string? Body = null);

/// <summary>What the Angular app needs to keep the &lt;head&gt; right after client-side navigation.</summary>
public sealed record SeoMetaDto(
    string Title,
    string Description,
    string? CanonicalUrl,
    string? ImageUrl,
    string OgType,
    bool NoIndex,
    int StatusCode,
    IReadOnlyList<string> JsonLd);

/// <summary>
/// The one place that decides each public page's title, description, canonical link, preview
/// image, robots rule and schema.org data — used for the first HTML response and, through
/// /api/seo/meta, by the Angular app on every client-side navigation, so the two never disagree.
/// <para>
/// The first response also carries the page's real content (headings, itinerary, dates, prices,
/// FAQ, links) inside &lt;app-root&gt;: crawlers that don't run JavaScript, and Google before it
/// renders, see a complete page instead of an empty shell. It is the same content the Angular page
/// shows; Angular replaces it when it starts.
/// </para>
/// </summary>
public partial class SeoPageRenderer(
    IWebHostEnvironment env,
    ITripService tripService,
    IDestinationService destinationService,
    IMemoryCache cache,
    IOptions<SeoOptions> seoOptions,
    IOptions<CompanyOptions> companyOptions,
    IOptions<OrganizerContactOptions> contactOptions,
    IOptions<FeatureOptions> featureOptions,
    ILogger<SeoPageRenderer> logger)
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);
    private static readonly JsonSerializerOptions JsonLdOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    // Customer pages that must never show up in search results (private, sign-in or placeholder).
    private static readonly string[] PrivatePrefixes = ["/admin", "/login", "/my-bookings", "/profile", "/hotels", "/driver"];

    private readonly SeoOptions _seo = seoOptions.Value;

    private string OrganizationId => Absolute("/") + "#organization";
    private string WebSiteId => Absolute("/") + "#website";

    // ---------- Entry points ----------

    public async Task RenderAsync(HttpContext context)
    {
        var shell = LoadShell();
        if (shell is null)
        {
            // No Angular build in wwwroot (e.g. running the API alone during development).
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var page = await GetPageAsync(context.Request.Path.Value ?? "/", context.RequestAborted);
        context.Response.StatusCode = page.StatusCode;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        if (page.NoIndex)
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex";
        }
        await context.Response.WriteAsync(Inject(shell, page), context.RequestAborted);
    }

    public async Task<SeoMetaDto> GetMetaAsync(string path, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync(path, cancellationToken);
        return new SeoMetaDto(
            page.Title,
            page.Description,
            page.CanonicalPath is null ? null : Absolute(page.CanonicalPath),
            AbsoluteOrNull(page.Image),
            page.OgType,
            page.NoIndex,
            page.StatusCode,
            (page.JsonLd ?? []).Select(block => JsonSerializer.Serialize(block, JsonLdOptions)).ToList());
    }

    /// <summary>
    /// Built pages are cached briefly per canonical path: seat counts move slowly enough for search
    /// engines, and it keeps crawler bursts off the database. Unknown paths are never cached, so
    /// random URLs can't fill the cache.
    /// </summary>
    private async Task<SeoPage> GetPageAsync(string path, CancellationToken cancellationToken)
    {
        var key = "seo:page:" + Normalize(path);
        if (cache.TryGetValue(key, out SeoPage? cached) && cached is not null)
        {
            return cached;
        }

        SeoPage page;
        try
        {
            page = await BuildPageAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never let an SEO problem take the site down: serve the plain shell instead.
            logger.LogError(ex, "Building SEO content for {Path} failed", path);
            return new SeoPage(_seo.DefaultTitle, _seo.DefaultDescription, null, _seo.DefaultImage);
        }

        if (page.StatusCode == StatusCodes.Status200OK && !page.NoIndex)
        {
            cache.Set(key, page, TimeSpan.FromMinutes(5));
        }
        return page;
    }

    private static string Normalize(string path)
    {
        var p = path.Split('?', '#')[0];
        return (p.Length > 1 ? p.TrimEnd('/') : p).ToLowerInvariant();
    }

    // ---------- Per-page content ----------

    private async Task<SeoPage> BuildPageAsync(string path, CancellationToken cancellationToken)
    {
        var lower = Normalize(path);

        if (lower is "/" or "")
        {
            return await HomePageAsync(cancellationToken);
        }

        if (lower.StartsWith("/trips/", StringComparison.Ordinal))
        {
            var tripId = SeoSlug.ParseTripId(lower["/trips/".Length..]);
            return tripId is null ? await NotFoundAsync(cancellationToken) : await TripPageAsync(tripId.Value, cancellationToken);
        }

        if (lower.StartsWith("/destinations/", StringComparison.Ordinal))
        {
            return await DestinationPageAsync(lower["/destinations/".Length..], cancellationToken);
        }

        if (lower == "/terms")
        {
            return TermsPage();
        }

        if (lower == "/cars" || lower.StartsWith("/cars/", StringComparison.Ordinal))
        {
            // Vehicle booking is switched off on the live site (Features:HideCarsAndHolidays) and its
            // pages redirect home, so they stay out of search until the feature is turned on.
            return new SeoPage($"Book Cars & Tempo Travellers with Driver | {_seo.SiteName}",
                "Book a car or tempo traveller with a driver in Odisha through Ghumo Odisha.",
                lower == "/cars" ? "/cars" : null, _seo.DefaultImage, NoIndex: featureOptions.Value.HideCarsAndHolidays || lower != "/cars");
        }

        if (PrivatePrefixes.Any(p => lower == p || lower.StartsWith(p + "/", StringComparison.Ordinal)))
        {
            return new SeoPage(_seo.SiteName, _seo.DefaultDescription, null, _seo.DefaultImage, NoIndex: true);
        }

        return await NotFoundAsync(cancellationToken);
    }

    private async Task<SeoPage> HomePageAsync(CancellationToken cancellationToken)
    {
        var trips = await AllTripsAsync(cancellationToken);
        var destinations = await destinationService.GetActiveDestinationsAsync(cancellationToken);
        var faqs = TripPageContent.BuildSiteFaqs(TripPageContent.HomeCity);
        var image = destinations.Select(d => d.HeroImageUrl ?? d.CoverImageUrl).FirstOrDefault(i => i is not null) ?? _seo.DefaultImage;

        var places = destinations.Take(5).Select(d => d.Name).ToList();
        var description = places.Count == 0
            ? _seo.DefaultDescription
            : Truncate($"Group trips from {TripPageContent.HomeCity} to {JoinAnd(places)} and more. Fixed dates and day-wise itineraries — book a seat online for {TripPageContent.Rupees(Application.Payments.BookingPaymentService.PerSeatAdvanceAmount)}.", 160);

        return new SeoPage(
            _seo.DefaultTitle,
            description,
            "/",
            image,
            JsonLd: [WebSiteSchema(), AgencySchema(image), TripListSchema(trips), FaqSchema(faqs)],
            Body: HomeBody(trips, destinations, faqs));
    }

    private async Task<SeoPage> TripPageAsync(int tripId, CancellationToken cancellationToken)
    {
        TripDetailDto trip;
        try
        {
            trip = await tripService.GetTripDetailAsync(tripId, cancellationToken);
        }
        catch (NotFoundException)
        {
            return await NotFoundAsync(cancellationToken);
        }

        var path = SeoSlug.TripPath(trip.TripId, trip.Title);
        var name = TripPageContent.DisplayName(trip.Title);
        var price = TripPageContent.Rupees(trip.AmountPerPerson);
        var allTrips = await AllTripsAsync(cancellationToken);
        var related = RelatedTrips(trip, allTrips);
        var image = trip.Photos.FirstOrDefault()?.ImageUrl ?? _seo.DefaultImage;
        var places = trip.PlacesCovered.Concat(trip.Highlights.Select(h => h.PlaceName)).Concat(trip.Destinations.Select(d => d.Name))
            .DistinctBy(p => p.ToLowerInvariant()).Take(5).ToList();
        var included = TripPageContent.IncludedItems(trip.Inclusions);

        var description = Truncate(
            $"{name}: {(trip.DurationLabel is null ? "" : trip.DurationLabel.ToLowerInvariant() + " ")}group trip" +
            (trip.DepartureCity is null ? "" : $" from {trip.DepartureCity}") + $", {price} per person." +
            (places.Count > 0 ? $" Covers {JoinAnd(places)}." : "") +
            (included.Count > 0 ? $" Includes {JoinAnd(included.Take(4).Select(i => i.ToLowerInvariant()).ToList())}." : ""), 160);

        var breadcrumbs = TripBreadcrumbs(trip, path);
        return new SeoPage(
            TripTitle(name, trip.DepartureCity, trip.DurationLabel),
            description,
            path,
            image,
            JsonLd: [TouristTripSchema(trip, path), BreadcrumbSchema(breadcrumbs), FaqSchema(trip.Faqs)],
            Body: TripBody(trip, breadcrumbs, related));
    }

    private async Task<SeoPage> DestinationPageAsync(string slug, CancellationToken cancellationToken)
    {
        DestinationDetailDto d;
        try
        {
            d = await destinationService.GetDestinationBySlugAsync(slug, cancellationToken);
        }
        catch (NotFoundException)
        {
            return await NotFoundAsync(cancellationToken);
        }

        var trips = (await destinationService.GetDestinationTripsAsync(d.Slug, 1, 50, null, null, cancellationToken)).Items;
        var path = $"/destinations/{d.Slug}";
        var about = Plain(d.AboutText ?? d.Tagline ?? d.KnownFor ?? "");
        var cheapest = trips.Count > 0 ? $" Trips from {TripPageContent.Rupees(trips.Min(t => t.AmountPerPerson))}." : "";
        var season = string.IsNullOrWhiteSpace(d.BestSeason) ? "" : $" Best time: {d.BestSeason}.";
        var description = Truncate($"{d.Name} group trips from {TripPageContent.HomeCity}.{cheapest}{season} {about}", 160);
        var faqs = d.Faqs;

        var jsonLd = new List<object>
        {
            TouristDestinationSchema(d, path),
            BreadcrumbSchema((d.Name, path)),
        };
        if (trips.Count > 0) jsonLd.Add(TripListSchema(trips));
        if (faqs.Count > 0) jsonLd.Add(FaqSchema(faqs));

        return new SeoPage(
            $"{d.Name} Trips from {TripPageContent.HomeCity} – Tour Packages | {_seo.SiteName}",
            description,
            path,
            d.HeroImageUrl ?? _seo.DefaultImage,
            JsonLd: jsonLd,
            Body: DestinationBody(d, trips, faqs));
    }

    private SeoPage TermsPage() => new(
        $"Trip Terms & Conditions | {_seo.SiteName}",
        "Booking, payment, cancellation and refund terms for Ghumo Odisha group trips: booking amount, 72-hour cancellation window, refunds, conduct and safety.",
        "/terms",
        _seo.DefaultImage,
        JsonLd: [BreadcrumbSchema(("Terms & Conditions", "/terms"))],
        Body: TermsBody());

    private async Task<SeoPage> NotFoundAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DestinationSummaryDto> destinations = [];
        try
        {
            destinations = await destinationService.GetActiveDestinationsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not load destinations for the 404 page");
        }

        return new SeoPage($"Page not found | {_seo.SiteName}", _seo.DefaultDescription, null, _seo.DefaultImage,
            NoIndex: true, StatusCode: StatusCodes.Status404NotFound, Body: NotFoundBody(destinations));
    }

    // ---------- Helpers for page content ----------

    private async Task<IReadOnlyList<TripSummaryDto>> AllTripsAsync(CancellationToken cancellationToken) =>
        (await tripService.GetActiveTripsAsync(1, 50, cancellationToken: cancellationToken)).Items;

    /// <summary>Other trips, those sharing a destination with this one first.</summary>
    private static List<TripSummaryDto> RelatedTrips(TripDetailDto trip, IReadOnlyList<TripSummaryDto> all)
    {
        var names = trip.Destinations.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return all.Where(t => t.TripId != trip.TripId)
            .OrderByDescending(t => t.DestinationNames.Any(names.Contains))
            .Take(4)
            .ToList();
    }

    private static (string Name, string Path)[] TripBreadcrumbs(TripDetailDto trip, string path)
    {
        var crumbs = new List<(string, string)>();
        var main = trip.Destinations.FirstOrDefault();
        if (main is not null)
        {
            crumbs.Add((main.Name, $"/destinations/{main.Slug}"));
        }
        crumbs.Add((TripPageContent.DisplayName(trip.Title), path));
        return [.. crumbs];
    }

    /// <summary>"Koraput Explore Trip from Bhubaneswar – 4D/3N | Ghumo Odisha", shortened to stay readable in results.</summary>
    private string TripTitle(string name, string? city, string? duration)
    {
        var hasNoun = Regex.IsMatch(name, @"\b(trip|tour|package)s?\b", RegexOptions.IgnoreCase);
        var core = name + (hasNoun ? "" : " Trip") + (city is null ? "" : $" from {city}");
        var shortDuration = duration is null ? null : Regex.Replace(duration, @"(\d+) Days? / (\d+) Nights?", "$1D/$2N");
        if (shortDuration == "1D/0N") shortDuration = "Day Trip";

        var full = shortDuration is null ? core : $"{core} – {shortDuration}";
        var branded = $"{full} | {_seo.SiteName}";
        if (branded.Length <= 65) return branded;
        return full.Length <= 65 ? full : $"{core} | {_seo.SiteName}".Length <= 65 ? $"{core} | {_seo.SiteName}" : core;
    }

    private static string CleanDuration(string text) => TripPageContent.CleanLabel(text);

    private static string JoinAnd(IReadOnlyList<string> items) => TripPageContent.JoinAnd(items);

    // ---------- schema.org blocks ----------

    private Dictionary<string, object?> WebSiteSchema() => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "WebSite",
        ["@id"] = WebSiteId,
        ["name"] = _seo.SiteName,
        ["url"] = Absolute("/"),
        ["inLanguage"] = "en-IN",
        ["publisher"] = new Dictionary<string, object?> { ["@id"] = OrganizationId },
    };

    private Dictionary<string, object?> AgencySchema(string? image)
    {
        var company = companyOptions.Value;
        var contact = contactOptions.Value;
        var phone = string.IsNullOrWhiteSpace(company.Phone) ? contact.Phone : company.Phone;
        var email = string.IsNullOrWhiteSpace(company.Email) ? contact.Email : company.Email;
        var sameAs = _seo.SameAs.Concat(string.IsNullOrWhiteSpace(contact.InstagramUrl) ? [] : [contact.InstagramUrl!])
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "TravelAgency",
            ["@id"] = OrganizationId,
            ["name"] = _seo.SiteName,
            ["url"] = Absolute("/"),
            ["logo"] = Absolute("/favicon.png"),
            ["image"] = AbsoluteOrNull(image ?? _seo.DefaultImage),
            ["description"] = _seo.DefaultDescription,
            ["telephone"] = string.IsNullOrWhiteSpace(phone) ? null : phone,
            ["email"] = string.IsNullOrWhiteSpace(email) ? null : email,
            ["address"] = string.IsNullOrWhiteSpace(_seo.AddressLocality) ? null : new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = _seo.StreetAddress,
                ["addressLocality"] = _seo.AddressLocality,
                ["addressRegion"] = _seo.AddressRegion,
                ["postalCode"] = _seo.PostalCode,
                ["addressCountry"] = "IN",
            },
            ["hasMap"] = company.DirectionsUrl(),
            ["areaServed"] = new Dictionary<string, object?> { ["@type"] = "State", ["name"] = "Odisha" },
            ["contactPoint"] = string.IsNullOrWhiteSpace(phone) ? null : new Dictionary<string, object?>
            {
                ["@type"] = "ContactPoint",
                ["telephone"] = phone,
                ["contactType"] = "customer service",
                ["areaServed"] = "IN",
            },
            ["sameAs"] = sameAs.Count > 0 ? sameAs : null,
        };
    }

    private Dictionary<string, object?> TouristTripSchema(TripDetailDto trip, string path)
    {
        var upcoming = trip.DateSlots;
        var open = upcoming.Where(s => !s.IsSoldOut && !s.IsBookingClosed).ToList();
        var stops = trip.Highlights.Select(h => (h.PlaceName, (string?)Truncate(h.Description, 200)))
            .Concat(trip.PlacesCovered.Select(p => (p, (string?)null)))
            .Concat(trip.Destinations.Select(d => (d.Name, (string?)null)))
            .DistinctBy(s => s.Item1.ToLowerInvariant())
            .ToList();

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "TouristTrip",
            ["name"] = trip.Title,
            ["description"] = Truncate(Plain(trip.Description), 500),
            ["url"] = Absolute(path),
            ["image"] = trip.Photos.Count == 0 ? null : trip.Photos.Take(6).Select(p => Absolute(p.ImageUrl)).ToList(),
            ["touristType"] = "Group travellers",
            ["itinerary"] = stops.Count == 0 ? null : new Dictionary<string, object?>
            {
                ["@type"] = "ItemList",
                ["numberOfItems"] = stops.Count,
                ["itemListElement"] = stops.Select((s, i) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = i + 1,
                    ["item"] = new Dictionary<string, object?> { ["@type"] = "TouristAttraction", ["name"] = s.Item1, ["description"] = s.Item2 },
                }).ToList(),
            },
            ["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = trip.AmountPerPerson.ToString("0.00", CultureInfo.InvariantCulture),
                ["priceCurrency"] = "INR",
                ["url"] = Absolute(path),
                ["availability"] = open.Count > 0 ? "https://schema.org/InStock" : "https://schema.org/SoldOut",
                ["validFrom"] = upcoming.FirstOrDefault()?.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["seller"] = new Dictionary<string, object?> { ["@id"] = OrganizationId },
            },
            ["provider"] = new Dictionary<string, object?> { ["@type"] = "TravelAgency", ["@id"] = OrganizationId, ["name"] = _seo.SiteName, ["url"] = Absolute("/") },
        };
    }

    private Dictionary<string, object?> TouristDestinationSchema(DestinationDetailDto d, string path) => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "TouristDestination",
        ["name"] = d.Name,
        ["description"] = Truncate(Plain(d.AboutText ?? d.Tagline ?? ""), 500),
        ["url"] = Absolute(path),
        ["image"] = AbsoluteOrNull(d.HeroImageUrl),
        ["containedInPlace"] = new Dictionary<string, object?>
        {
            ["@type"] = "State",
            ["name"] = d.Region is not null && d.Region.Contains("Andhra", StringComparison.OrdinalIgnoreCase) ? "Andhra Pradesh" : "Odisha",
        },
    };

    private Dictionary<string, object?> TripListSchema(IReadOnlyList<TripSummaryDto> trips) => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "ItemList",
        ["itemListElement"] = trips.Select((t, i) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["url"] = Absolute(SeoSlug.TripPath(t.TripId, t.Title)),
            ["name"] = t.Title,
        }).ToList(),
    };

    private static Dictionary<string, object?> FaqSchema(IReadOnlyList<FaqItem> faqs) => new()
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "FAQPage",
        ["mainEntity"] = faqs.Select(f => new Dictionary<string, object?>
        {
            ["@type"] = "Question",
            ["name"] = f.Question,
            ["acceptedAnswer"] = new Dictionary<string, object?> { ["@type"] = "Answer", ["text"] = f.Answer },
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
        Meta(head, "name", "robots", page.NoIndex ? "noindex, nofollow" : "index, follow, max-image-preview:large");
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
            head.Append("  <script type=\"application/ld+json\" data-seo>").Append(JsonSerializer.Serialize(block, JsonLdOptions)).Append("</script>\n");
        }
        if (page.Body is not null)
        {
            head.Append("  <style>").Append(PrerenderCss).Append("</style>\n");
        }

        var cleaned = TitleTag().Replace(shell, "", 1);
        cleaned = DescriptionTag().Replace(cleaned, "", 1);
        var headEnd = cleaned.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var html = headEnd < 0 ? cleaned : cleaned.Insert(headEnd, head.ToString());

        // Angular clears <app-root> when it starts, so this content is only what shows (and what
        // crawlers read) until the app takes over with the same page.
        return page.Body is null ? html : AppRoot().Replace(html, m => m.Groups[1].Value + page.Body + "</app-root>", 1);
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

    [GeneratedRegex(@"(<app-root[^>]*>)\s*</app-root>", RegexOptions.IgnoreCase)]
    private static partial Regex AppRoot();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
