namespace GhumoOdisha.Application.Seo;

/// <param name="Images">Site paths of this page's photos, listed for Google Images in the sitemap.</param>
public record SitemapEntry(string Path, DateTime? LastModifiedUtc, IReadOnlyList<string>? Images = null);
