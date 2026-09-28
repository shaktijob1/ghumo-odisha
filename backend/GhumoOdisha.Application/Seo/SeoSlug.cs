using System.Text.RegularExpressions;

namespace GhumoOdisha.Application.Seo;

/// <summary>
/// Readable trip URLs: <c>/trips/1-puri-konark-satapada</c>. The leading number is what the site
/// actually looks up, so renaming a trip never breaks an old link — the words are only for people
/// and search engines. Mirrors <c>tripPath()</c> in the Angular app; keep the two in sync.
/// </summary>
public static partial class SeoSlug
{
    public static string Slugify(string text) =>
        NonAlphanumeric().Replace(text.ToLowerInvariant(), "-").Trim('-');

    public static string TripPath(int tripId, string title)
    {
        var slug = Slugify(title);
        return slug.Length == 0 ? $"/trips/{tripId}" : $"/trips/{tripId}-{slug}";
    }

    /// <summary>The trip id at the start of a /trips/{segment} path segment ("12-puri-trip" → 12).</summary>
    public static int? ParseTripId(string segment)
    {
        var match = LeadingNumber().Match(segment);
        return match.Success && int.TryParse(match.Value, out var id) ? id : null;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^\d+(?=-|$)")]
    private static partial Regex LeadingNumber();
}
