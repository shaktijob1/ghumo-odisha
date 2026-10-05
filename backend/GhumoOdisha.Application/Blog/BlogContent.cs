using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Trips;

namespace GhumoOdisha.Application.Blog;

/// <summary>
/// Turns the admin's plain-text article into headings, paragraphs and bullet lists — the same
/// blocks the Angular page and the server-rendered page show, so no HTML is ever stored or trusted.
/// </summary>
public static class BlogContent
{
    public const int MaxTags = 120;
    public const int MaxTagLength = 80;

    public static IReadOnlyList<BlogBlock> Parse(string? content)
    {
        var blocks = new List<BlogBlock>();
        var paragraph = new List<string>();
        var bullets = new List<string>();

        void Flush()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new BlogBlock("p", string.Join(" ", paragraph)));
                paragraph.Clear();
            }
            if (bullets.Count > 0)
            {
                blocks.Add(new BlogBlock("ul", null, bullets.ToList()));
                bullets.Clear();
            }
        }

        foreach (var raw in (content ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                Flush();
            }
            else if (line.StartsWith("#"))
            {
                Flush();
                var heading = line.TrimStart('#').Trim();
                if (heading.Length > 0) blocks.Add(new BlogBlock("h2", heading));
            }
            else if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("• "))
            {
                if (paragraph.Count > 0) Flush();
                bullets.Add(line[2..].Trim());
            }
            else
            {
                if (bullets.Count > 0) Flush();
                paragraph.Add(line);
            }
        }
        Flush();
        return blocks;
    }

    /// <summary>Minutes to read at about 200 words a minute (at least 1).</summary>
    public static int ReadMinutes(string? content)
    {
        var words = (content ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / 200.0));
    }

    /// <summary>Tags as entered: trimmed, blanks and repeats (any letter case) dropped. Commas split too.</summary>
    public static IReadOnlyList<string> CleanTags(IEnumerable<string>? tags) =>
        TripSearch.CleanPlaces((tags ?? []).SelectMany(t => (t ?? "").Split(',')));

    public static string? JoinTags(IEnumerable<string>? tags)
    {
        var clean = CleanTags(tags);
        return clean.Count == 0 ? null : string.Join("\n", clean);
    }

    public static IReadOnlyList<string> ParseTags(string? stored) => TripSearch.ParsePlaces(stored);
}
