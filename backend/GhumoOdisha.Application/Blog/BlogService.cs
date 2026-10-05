using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Blog;

public class BlogService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : IBlogService
{
    private const int MoreStoriesCount = 3;

    // ---------- Public ----------

    public async Task<IReadOnlyList<BlogPostSummaryDto>> GetPublishedAsync(int limit, CancellationToken cancellationToken = default)
    {
        var posts = await db.BlogPosts.AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.BlogPostId)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(p => new
            {
                p.BlogPostId, p.Title, p.Slug, p.Place, p.Excerpt, p.HeroImageUrl, p.PublishedAt, p.Content,
                FirstPhoto = p.Photos.OrderBy(ph => ph.DisplayOrder).Select(ph => ph.ImageUrl).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return posts.Select(p => new BlogPostSummaryDto(
                p.BlogPostId, p.Title, p.Slug, p.Place, p.Excerpt, p.HeroImageUrl ?? p.FirstPhoto, p.PublishedAt, BlogContent.ReadMinutes(p.Content)))
            .ToList();
    }

    public async Task<BlogPostDetailDto> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var post = await db.BlogPosts.AsNoTracking()
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, cancellationToken)
            ?? throw new NotFoundException("Story not found.");

        var more = (await GetPublishedAsync(MoreStoriesCount + 1, cancellationToken))
            .Where(s => s.BlogPostId != post.BlogPostId)
            .Take(MoreStoriesCount)
            .ToList();

        var photos = MapPhotos(post);
        return new BlogPostDetailDto(
            post.BlogPostId,
            post.Title,
            post.Slug,
            post.Place,
            post.Excerpt,
            post.HeroImageUrl ?? photos.FirstOrDefault()?.ImageUrl,
            post.PublishedAt,
            post.UpdatedAt,
            BlogContent.ReadMinutes(post.Content),
            BlogContent.Parse(post.Content),
            BlogContent.ParseTags(post.Tags),
            photos,
            more);
    }

    // ---------- Admin ----------

    public async Task<IReadOnlyList<AdminBlogPostListItemDto>> GetAdminPostsAsync(CancellationToken cancellationToken = default)
    {
        var posts = await db.BlogPosts.AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new
            {
                p.BlogPostId, p.Title, p.Slug, p.Place, p.IsPublished, p.PublishedAt, p.UpdatedAt, p.HeroImageUrl, p.Tags,
                PhotoCount = p.Photos.Count,
            })
            .ToListAsync(cancellationToken);

        return posts.Select(p => new AdminBlogPostListItemDto(
                p.BlogPostId, p.Title, p.Slug, p.Place, p.IsPublished, p.PublishedAt, p.UpdatedAt, p.HeroImageUrl, p.PhotoCount,
                BlogContent.ParseTags(p.Tags).Count))
            .ToList();
    }

    public async Task<AdminBlogPostDetailDto> GetAdminPostAsync(int blogPostId, CancellationToken cancellationToken = default)
    {
        var post = await db.BlogPosts.AsNoTracking()
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.BlogPostId == blogPostId, cancellationToken)
            ?? throw new NotFoundException("Story not found.");

        return new AdminBlogPostDetailDto(
            post.BlogPostId, post.Title, post.Slug, post.Place, post.Excerpt, post.Content, post.HeroImageUrl,
            BlogContent.ParseTags(post.Tags), post.IsPublished, post.PublishedAt, post.CreatedAt, post.UpdatedAt, MapPhotos(post));
    }

    public async Task<int> CreateAsync(SaveBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSlugFreeAsync(request.Slug, null, cancellationToken);

        var now = DateTime.UtcNow;
        var post = new BlogPost { CreatedAt = now };
        Apply(post, request, now);
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync(cancellationToken);
        return post.BlogPostId;
    }

    public async Task UpdateAsync(int blogPostId, SaveBlogPostRequest request, CancellationToken cancellationToken = default)
    {
        var post = await FindAsync(blogPostId, cancellationToken);
        await EnsureSlugFreeAsync(request.Slug, blogPostId, cancellationToken);
        Apply(post, request, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int blogPostId, CancellationToken cancellationToken = default)
    {
        var post = await db.BlogPosts.Include(p => p.Photos).FirstOrDefaultAsync(p => p.BlogPostId == blogPostId, cancellationToken)
            ?? throw new NotFoundException("Story not found.");

        var files = post.Photos.Select(p => p.ImageUrl).Append(post.HeroImageUrl).OfType<string>().ToList();
        db.BlogPosts.Remove(post);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var file in files)
        {
            imageStorage.Delete(file);
        }
    }

    public async Task SetHeroImageAsync(int blogPostId, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var post = await FindAsync(blogPostId, cancellationToken);
        UploadedImageRules.Validate(image);

        var oldUrl = post.HeroImageUrl;
        post.HeroImageUrl = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "blog", cancellationToken);
        post.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(oldUrl))
        {
            imageStorage.Delete(oldUrl);
        }
    }

    public async Task<BlogPhotoDto> AddPhotoAsync(int blogPostId, UploadedImage image, string? caption, CancellationToken cancellationToken = default)
    {
        var post = await db.BlogPosts.Include(p => p.Photos).FirstOrDefaultAsync(p => p.BlogPostId == blogPostId, cancellationToken)
            ?? throw new NotFoundException("Story not found.");
        UploadedImageRules.Validate(image);
        var cleanCaption = UploadedImageRules.CleanCaption(caption);

        if (post.Photos.Count >= IBlogService.MaxPhotosPerPost)
        {
            throw new ValidationAppException([$"A story can have up to {IBlogService.MaxPhotosPerPost} photos. Delete one to add another."]);
        }

        var photo = new BlogPostPhoto
        {
            ImageUrl = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "blog", cancellationToken),
            Caption = cleanCaption,
            DisplayOrder = post.Photos.Count == 0 ? 0 : post.Photos.Max(p => p.DisplayOrder) + 1,
        };
        post.Photos.Add(photo);
        post.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new BlogPhotoDto(photo.BlogPostPhotoId, photo.ImageUrl, photo.Caption);
    }

    public async Task DeletePhotoAsync(int blogPostId, int blogPostPhotoId, CancellationToken cancellationToken = default)
    {
        var photo = await db.BlogPostPhotos.FirstOrDefaultAsync(p => p.BlogPostPhotoId == blogPostPhotoId && p.BlogPostId == blogPostId, cancellationToken)
            ?? throw new NotFoundException("Photo not found.");

        db.BlogPostPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(photo.ImageUrl);
    }

    // ---------- Helpers ----------

    private static void Apply(BlogPost post, SaveBlogPostRequest request, DateTime now)
    {
        var tags = BlogContent.CleanTags(request.Tags);
        if (tags.Count > BlogContent.MaxTags)
        {
            throw new ValidationAppException([$"Add at most {BlogContent.MaxTags} tags."]);
        }
        if (tags.Any(t => t.Length > BlogContent.MaxTagLength))
        {
            throw new ValidationAppException([$"Each tag must be {BlogContent.MaxTagLength} characters or fewer."]);
        }

        post.Title = request.Title.Trim();
        post.Slug = request.Slug.Trim();
        post.Place = string.IsNullOrWhiteSpace(request.Place) ? null : request.Place.Trim();
        post.Excerpt = request.Excerpt.Trim();
        post.Content = request.Content.Trim();
        post.Tags = BlogContent.JoinTags(tags);
        post.IsPublished = request.IsPublished;
        if (request.IsPublished && post.PublishedAt is null)
        {
            post.PublishedAt = now;
        }
        post.UpdatedAt = now;
    }

    private async Task EnsureSlugFreeAsync(string slug, int? exceptId, CancellationToken cancellationToken)
    {
        var taken = await db.BlogPosts.AnyAsync(p => p.Slug == slug && (exceptId == null || p.BlogPostId != exceptId), cancellationToken);
        if (taken)
        {
            throw new ConflictException("A story with this slug already exists.");
        }
    }

    private async Task<BlogPost> FindAsync(int blogPostId, CancellationToken cancellationToken) =>
        await db.BlogPosts.FirstOrDefaultAsync(p => p.BlogPostId == blogPostId, cancellationToken)
            ?? throw new NotFoundException("Story not found.");

    private static List<BlogPhotoDto> MapPhotos(BlogPost post) =>
        post.Photos.OrderBy(p => p.DisplayOrder).ThenBy(p => p.BlogPostPhotoId)
            .Select(p => new BlogPhotoDto(p.BlogPostPhotoId, p.ImageUrl, p.Caption))
            .ToList();
}
