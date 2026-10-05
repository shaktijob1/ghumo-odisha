using System.Globalization;
using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Exceptions;

namespace GhumoOdisha.Api.Seo;

/// <summary>The "News &amp; Blog" pages: /blog (every story) and /blog/{slug} (one story).</summary>
public partial class SeoPageRenderer
{
    /// <summary>Stories listed on the home page (the Angular home page asks for the same number).</summary>
    private const int HomeStoryCount = 6;

    private const string BlogTitle = "Travel Stories & Odisha Travel Guides";

    // ---------- Pages ----------

    private async Task<SeoPage> BlogListPageAsync(CancellationToken cancellationToken)
    {
        var stories = await blogService.GetPublishedAsync(100, cancellationToken);
        var image = stories.Select(s => s.HeroImageUrl).FirstOrDefault(i => i is not null) ?? _seo.DefaultImage;

        var jsonLd = new List<object> { BreadcrumbSchema(("Travel stories", "/blog")) };
        if (stories.Count > 0)
        {
            jsonLd.Add(new Dictionary<string, object?>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "ItemList",
                ["itemListElement"] = stories.Select((s, i) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = i + 1,
                    ["url"] = Absolute(BlogPath(s.Slug)),
                    ["name"] = s.Title,
                }).ToList(),
            });
        }

        return new SeoPage(
            $"{BlogTitle} | {_seo.SiteName}",
            "Inspiring travel stories and guides from Ghumo Odisha: the best places to visit in Koraput, Mahendragiri and across Odisha, when to go and how to get there.",
            "/blog",
            image,
            JsonLd: jsonLd,
            Body: BlogListBody(stories));
    }

    private async Task<SeoPage> BlogPostPageAsync(string slug, CancellationToken cancellationToken)
    {
        BlogPostDetailDto post;
        try
        {
            post = await blogService.GetPublishedBySlugAsync(slug, cancellationToken);
        }
        catch (NotFoundException)
        {
            return await NotFoundAsync(cancellationToken);
        }

        var path = BlogPath(post.Slug);
        var breadcrumbs = new[] { ("Travel stories", "/blog"), (post.Title, path) };
        var branded = $"{post.Title} | {_seo.SiteName}";

        return new SeoPage(
            branded.Length <= 70 ? branded : post.Title,
            Truncate(post.Excerpt, 160),
            path,
            post.HeroImageUrl ?? _seo.DefaultImage,
            OgType: "article",
            JsonLd: [BlogPostingSchema(post, path), BreadcrumbSchema(breadcrumbs)],
            Body: BlogPostBody(post, breadcrumbs));
    }

    private static string BlogPath(string slug) => $"/blog/{slug}";

    // ---------- schema.org ----------

    private Dictionary<string, object?> BlogPostingSchema(BlogPostDetailDto post, string path)
    {
        var images = new[] { post.HeroImageUrl }.Concat(post.Photos.Select(p => p.ImageUrl))
            .OfType<string>().Distinct().Take(6).Select(Absolute).ToList();

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BlogPosting",
            ["headline"] = post.Title,
            ["description"] = Truncate(post.Excerpt, 300),
            ["url"] = Absolute(path),
            ["mainEntityOfPage"] = Absolute(path),
            ["image"] = images.Count == 0 ? AbsoluteOrNull(_seo.DefaultImage) : images,
            ["datePublished"] = post.PublishedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["dateModified"] = post.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["inLanguage"] = "en-IN",
            ["keywords"] = post.Tags.Count == 0 ? null : string.Join(", ", post.Tags),
            ["about"] = post.Place is null ? null : new Dictionary<string, object?> { ["@type"] = "Place", ["name"] = post.Place },
            ["author"] = new Dictionary<string, object?> { ["@type"] = "Organization", ["@id"] = OrganizationId, ["name"] = _seo.SiteName, ["url"] = Absolute("/") },
            ["publisher"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["@id"] = OrganizationId,
                ["name"] = _seo.SiteName,
                ["logo"] = new Dictionary<string, object?> { ["@type"] = "ImageObject", ["url"] = Absolute("/favicon.png") },
            },
        };
    }

    // ---------- HTML ----------

    private static string Img(string src, string alt, string? cls = null) =>
        $"<img{(cls is null ? "" : $" class=\"{cls}\"")} src=\"{Html.Encode(src)}\" alt=\"{Html.Encode(alt)}\" loading=\"lazy\" style=\"max-width:100%\">";

    private static void StoryCards(HtmlWriter w, IReadOnlyList<BlogPostSummaryDto> stories)
    {
        w.Raw("<ul class=\"ssr-cards\">");
        foreach (var s in stories)
        {
            w.Raw("<li>").Link(BlogPath(s.Slug), s.Title).Raw("<div class=\"ssr-muted\">");
            if (s.Place is not null) w.Text(s.Place + " · ");
            w.Text($"{s.ReadMinutes} min read").Raw("<br>").Text(s.Excerpt).Raw("</div></li>");
        }
        w.Raw("</ul>");
    }

    private string BlogListBody(IReadOnlyList<BlogPostSummaryDto> stories) => Page(w =>
    {
        w.Tag("h1", BlogTitle)
         .Tag("p", "Inspiring travel stories from our group trips, and honest guides to the best places to visit in Odisha.");
        if (stories.Count == 0) w.Tag("p", "No stories yet — check back soon.", "ssr-muted");
        else StoryCards(w, stories);
    }, [("Travel stories", "/blog")]);

    private string BlogPostBody(BlogPostDetailDto post, (string Name, string Path)[] breadcrumbs) => Page(w =>
    {
        if (post.HeroImageUrl is not null)
        {
            w.Raw($"<img class=\"ssr-hero\" src=\"{Html.Encode(post.HeroImageUrl)}\" alt=\"{Html.Encode(post.Title)}\" width=\"1280\" height=\"560\" fetchpriority=\"high\">");
        }
        if (post.Place is not null) w.Tag("p", post.Place, "ssr-muted");
        w.Tag("h1", post.Title);
        var meta = $"{post.ReadMinutes} min read";
        if (post.PublishedAt is { } published) meta = published.ToString("d MMMM yyyy", India) + " · " + meta;
        w.Tag("p", meta, "ssr-muted").Tag("p", post.Excerpt);

        w.Raw("<article class=\"ssr-sec\">");
        foreach (var block in post.Blocks)
        {
            switch (block.Type)
            {
                case "h2": w.Tag("h2", block.Text); break;
                case "ul":
                    w.Raw("<ul class=\"ssr-list\">");
                    foreach (var item in block.Items ?? []) w.Tag("li", item);
                    w.Raw("</ul>");
                    break;
                default: w.Tag("p", block.Text); break;
            }
        }
        w.Raw("</article>");

        if (post.Photos.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", $"{post.Place ?? "Trip"} photos").Raw("<ul class=\"ssr-photos\">");
            foreach (var p in post.Photos) w.Raw("<li>").Raw(Img(p.ImageUrl, p.Caption ?? post.Title)).Raw("</li>");
            w.Raw("</ul></section>");
        }

        if (post.Tags.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Popular tags").Raw("<ul class=\"ssr-tags\">");
            foreach (var tag in post.Tags) w.Tag("li", tag);
            w.Raw("</ul></section>");
        }

        w.Raw("<p>").Link("/#upcoming-trips", "See upcoming group trips").Raw("</p>");

        if (post.MoreStories.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "More travel stories");
            StoryCards(w, post.MoreStories);
            w.Raw("</section>");
        }
    }, breadcrumbs);
}
