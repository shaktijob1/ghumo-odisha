using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Application.Blog;

public interface IBlogService
{
    /// <summary>Most extra photos one story can hold (besides its hero).</summary>
    const int MaxPhotosPerPost = 12;

    // Public: published stories only, newest first.
    Task<IReadOnlyList<BlogPostSummaryDto>> GetPublishedAsync(int limit, CancellationToken cancellationToken = default);

    Task<BlogPostDetailDto> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    // Admin
    Task<IReadOnlyList<AdminBlogPostListItemDto>> GetAdminPostsAsync(CancellationToken cancellationToken = default);

    Task<AdminBlogPostDetailDto> GetAdminPostAsync(int blogPostId, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(SaveBlogPostRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(int blogPostId, SaveBlogPostRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int blogPostId, CancellationToken cancellationToken = default);

    Task SetHeroImageAsync(int blogPostId, UploadedImage image, CancellationToken cancellationToken = default);

    Task<BlogPhotoDto> AddPhotoAsync(int blogPostId, UploadedImage image, string? caption, CancellationToken cancellationToken = default);

    Task DeletePhotoAsync(int blogPostId, int blogPostPhotoId, CancellationToken cancellationToken = default);
}
