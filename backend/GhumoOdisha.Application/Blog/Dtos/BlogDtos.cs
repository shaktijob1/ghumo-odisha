namespace GhumoOdisha.Application.Blog.Dtos;

public record BlogPostSummaryDto(
    int BlogPostId,
    string Title,
    string Slug,
    string? Place,
    string Excerpt,
    string? HeroImageUrl,
    DateTime? PublishedAt,
    int ReadMinutes);

/// <summary>One piece of the article: a heading ("h2"), a paragraph ("p") or a bullet list ("ul", in Items).</summary>
public record BlogBlock(string Type, string? Text, IReadOnlyList<string>? Items = null);

public record BlogPhotoDto(int BlogPostPhotoId, string ImageUrl, string? Caption);

public record BlogPostDetailDto(
    int BlogPostId,
    string Title,
    string Slug,
    string? Place,
    string Excerpt,
    string? HeroImageUrl,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    int ReadMinutes,
    IReadOnlyList<BlogBlock> Blocks,
    IReadOnlyList<string> Tags,
    IReadOnlyList<BlogPhotoDto> Photos,
    IReadOnlyList<BlogPostSummaryDto> MoreStories);

public record AdminBlogPostListItemDto(
    int BlogPostId,
    string Title,
    string Slug,
    string? Place,
    bool IsPublished,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    string? HeroImageUrl,
    int PhotoCount,
    int TagCount);

public record AdminBlogPostDetailDto(
    int BlogPostId,
    string Title,
    string Slug,
    string? Place,
    string Excerpt,
    string Content,
    string? HeroImageUrl,
    IReadOnlyList<string> Tags,
    bool IsPublished,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<BlogPhotoDto> Photos);

public record SaveBlogPostRequest(
    string Title,
    string Slug,
    string? Place,
    string Excerpt,
    string Content,
    IReadOnlyList<string>? Tags,
    bool IsPublished);
