using System.Globalization;
using System.Security;
using System.Text;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Seo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Seo;

public sealed record HomeContentDto(string HeadingSub, string Intro, IReadOnlyList<FaqItem> BookingSteps, IReadOnlyList<FaqItem> Faqs);

public static class SeoEndpoints
{
    // Public pages whose addresses are all lower case — mixed-case variants are permanently redirected.
    private static readonly string[] LowerCasePrefixes = ["/trips", "/destinations", "/terms"];

    /// <summary>
    /// One official address per page, reached in a single permanent (301) redirect:
    /// www.ghumoodisha.com → ghumoodisha.com, "/terms/" → "/terms", "/Terms" → "/terms", the retired
    /// "/trips" and "/destinations" list pages → the home page (which lists every trip), and an old or
    /// shortened trip link ("/trips/6", "/trips/6-old-name") → that trip's current "/trips/6-koraput-explore".
    /// API calls, uploaded files and the app's script/style files are never touched.
    /// </summary>
    public static void UseSeoRedirects(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var request = context.Request;
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                await next();
                return;
            }

            var site = new Uri(context.RequestServices.GetRequiredService<IOptions<SeoOptions>>().Value.SiteUrl);
            var wrongHost = request.Host.Host.Equals("www." + site.Host, StringComparison.OrdinalIgnoreCase);

            var path = request.Path.Value ?? "/";
            var target = path;
            var isPage = !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
                && !Path.HasExtension(path);

            if (isPage)
            {
                if (target.Length > 1)
                {
                    target = target.TrimEnd('/');
                    if (target.Length == 0) target = "/";
                }

                var lower = target.ToLowerInvariant();
                if (target != lower && LowerCasePrefixes.Any(p => lower == p || lower.StartsWith(p + "/", StringComparison.Ordinal)))
                {
                    target = lower;
                }

                if (target is "/trips" or "/destinations")
                {
                    target = "/";
                }

                if (target.StartsWith("/trips/", StringComparison.Ordinal) && SeoSlug.ParseTripId(target["/trips/".Length..]) is { } tripId)
                {
                    var official = await TripPathAsync(context, tripId);
                    if (official is not null) target = official;
                }
            }

            if (!wrongHost && target == path)
            {
                await next();
                return;
            }

            var location = (wrongHost ? site.GetLeftPart(UriPartial.Authority) : "") + target + request.QueryString.Value;
            context.Response.Redirect(location, permanent: true);
        });
    }

    private static async Task<string?> TripPathAsync(HttpContext context, int tripId)
    {
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
        var key = $"seo:trip-path:{tripId}";
        if (cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        var path = await context.RequestServices.GetRequiredService<ISeoService>().GetTripPathAsync(tripId, context.RequestAborted);
        cache.Set(key, path, TimeSpan.FromMinutes(5));
        return path;
    }

    /// <summary>robots.txt, sitemap.xml, page metadata for the app, and the Angular page shell with per-page SEO content.</summary>
    public static void MapSeo(this WebApplication app)
    {
        app.MapGet("/robots.txt", (IOptions<SeoOptions> options) =>
        {
            var site = options.Value.SiteUrl.TrimEnd('/');
            // Public /api stays crawlable on purpose: Google fetches it while rendering the trip pages.
            // Private areas are blocked here and also answer with noindex.
            var body = $"""
                User-agent: *
                Allow: /
                Disallow: /admin
                Disallow: /driver
                Disallow: /login
                Disallow: /my-bookings
                Disallow: /profile
                Disallow: /hotels
                Disallow: /cars/
                Disallow: /api/admin/
                Disallow: /api/auth/
                Disallow: /api/customer/
                Disallow: /api/driver
                Disallow: /api/partner
                Disallow: /api/search-logs

                Sitemap: {site}/sitemap.xml

                """;
            return Results.Text(body, "text/plain", Encoding.UTF8);
        });

        app.MapGet("/sitemap.xml", async (ISeoService seo, IMemoryCache cache, IOptions<SeoOptions> options, CancellationToken cancellationToken) =>
        {
            var xml = await cache.GetOrCreateAsync("seo:sitemap", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
                var site = options.Value.SiteUrl.TrimEnd('/');
                string Absolute(string path) => path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : site + "/" + path.TrimStart('/');

                var sb = new StringBuilder();
                sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
                sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:image="http://www.google.com/schemas/sitemap-image/1.1">""");
                foreach (var e in await seo.GetSitemapEntriesAsync(cancellationToken))
                {
                    sb.Append("  <url><loc>").Append(SecurityElement.Escape(Absolute(e.Path))).Append("</loc>");
                    if (e.LastModifiedUtc is { } modified)
                    {
                        sb.Append("<lastmod>").Append(modified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</lastmod>");
                    }
                    foreach (var image in e.Images ?? [])
                    {
                        sb.Append("<image:image><image:loc>").Append(SecurityElement.Escape(Absolute(image))).Append("</image:loc></image:image>");
                    }
                    sb.AppendLine("</url>");
                }
                sb.AppendLine("</urlset>");
                return sb.ToString();
            });
            return Results.Text(xml!, "application/xml", Encoding.UTF8);
        });

        // The Angular app asks for the same title/description/canonical/structured data the first
        // response carried, after each client-side navigation — one source of truth for both.
        app.MapGet("/api/seo/meta", async (string? path, SeoPageRenderer renderer, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.Length > 300)
            {
                return Results.BadRequest(ApiResponse<object>.Fail("A site path starting with / is required."));
            }
            return Results.Ok(ApiResponse<SeoMetaDto>.Ok(await renderer.GetMetaAsync(path, cancellationToken)));
        });

        // The home page's "About", "How booking works" and FAQ text — the same the first response
        // carries — so the Angular home page and the server-rendered one never drift apart.
        app.MapGet("/api/site/home-content", () => Results.Ok(ApiResponse<HomeContentDto>.Ok(new HomeContentDto(
            TripPageContent.HomeHeadingSub,
            TripPageContent.HomeIntro,
            TripPageContent.BookingSteps,
            TripPageContent.BuildSiteFaqs(TripPageContent.HomeCity)))));

        // Home page and every client-side route (anything without a file extension, outside /api)
        // get the Angular shell with that page's SEO content; unknown /api/* and missing files stay 404.
        app.MapGet("/", (SeoPageRenderer renderer, HttpContext context) => renderer.RenderAsync(context));
        app.MapFallback("{*path:nonfile:regex(^(?!api).*$)}", (SeoPageRenderer renderer, HttpContext context) => renderer.RenderAsync(context));
    }
}
