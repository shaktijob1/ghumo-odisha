using System.Globalization;
using System.Security;
using System.Text;
using GhumoOdisha.Application.Seo;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Seo;

public static class SeoEndpoints
{
    /// <summary>robots.txt, sitemap.xml, and the Angular page shell with per-page SEO tags.</summary>
    public static void MapSeo(this WebApplication app)
    {
        app.MapGet("/robots.txt", (IOptions<SeoOptions> options) =>
        {
            var site = options.Value.SiteUrl.TrimEnd('/');
            // /api stays crawlable on purpose: Google fetches it while rendering the trip pages.
            var body = $"""
                User-agent: *
                Allow: /
                Disallow: /admin
                Disallow: /login
                Disallow: /my-bookings
                Disallow: /profile

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
                var sb = new StringBuilder();
                sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
                sb.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");
                foreach (var e in await seo.GetSitemapEntriesAsync(cancellationToken))
                {
                    sb.Append("  <url><loc>").Append(SecurityElement.Escape(site + e.Path)).Append("</loc>");
                    if (e.LastModifiedUtc is { } modified)
                    {
                        sb.Append("<lastmod>").Append(modified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("</lastmod>");
                    }
                    sb.AppendLine("</url>");
                }
                sb.AppendLine("</urlset>");
                return sb.ToString();
            });
            return Results.Text(xml!, "application/xml", Encoding.UTF8);
        });

        // Home page and every client-side route (anything without a file extension, outside /api)
        // get the Angular shell with that page's SEO tags; unknown /api/* and missing files stay 404.
        app.MapGet("/", (SeoPageRenderer renderer, HttpContext context) => renderer.RenderAsync(context));
        app.MapFallback("{*path:nonfile:regex(^(?!api).*$)}", (SeoPageRenderer renderer, HttpContext context) => renderer.RenderAsync(context));
    }
}
