namespace GhumoOdisha.Domain.Entities;

/// <summary>An extra photo shown in a blog post's photo strip, below its hero.</summary>
public class BlogPostPhoto
{
    public int BlogPostPhotoId { get; set; }
    public int BlogPostId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }

    public BlogPost BlogPost { get; set; } = null!;
}
