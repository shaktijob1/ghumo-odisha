namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A travel story / blog article, e.g. "Best places to visit in Koraput", shown at /blog/{Slug}.
/// </summary>
public class BlogPost
{
    public int BlogPostId { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;

    /// <summary>The place the story is about ("Koraput") — shown above the title.</summary>
    public string? Place { get; set; }

    /// <summary>One or two sentences for cards, the meta description and link previews.</summary>
    public string Excerpt { get; set; } = null!;

    /// <summary>Article text: paragraphs separated by a blank line, "## " starts a heading, "- " a bullet.</summary>
    public string Content { get; set; } = null!;
    public string? HeroImageUrl { get; set; }

    /// <summary>Search phrases shown under "Popular tags", one per line.</summary>
    public string? Tags { get; set; }
    public bool IsPublished { get; set; }

    /// <summary>Set the first time the story is published.</summary>
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<BlogPostPhoto> Photos { get; set; } = new List<BlogPostPhoto>();
}
