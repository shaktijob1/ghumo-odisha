using GhumoOdisha.Application.Blog;
using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

/// <summary>Remembers what was saved and deleted instead of touching the disk.</summary>
internal sealed class FakeImageStorage : IImageStorage
{
    public List<string> Saved { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task<string> SaveAsync(Stream content, string fileName, string contentType, string subFolder, CancellationToken cancellationToken = default)
    {
        var url = $"/uploads/{subFolder}/{Guid.NewGuid():N}.jpg";
        Saved.Add(url);
        return Task.FromResult(url);
    }

    public void Delete(string relativeUrl) => Deleted.Add(relativeUrl);

    public Stream? OpenRead(string relativeUrl) => null;
}

/// <summary>Travel stories (/blog) and the home page's "Real Travel Moments" gallery.</summary>
public class BlogAndTravelMomentTests
{
    private static UploadedImage Jpeg() => new(new MemoryStream([0xFF, 0xD8, 0xFF]), "photo.jpg", "image/jpeg", 3);

    private static SaveBlogPostRequest Story(string slug, bool published = true, IReadOnlyList<string>? tags = null) => new(
        "Test story " + slug, slug, "Koraput", "Short summary.", "Intro paragraph.\n\n## Heading\nBody text.", tags ?? ["Koraput trip"], published);

    private static string RandomSlug() => "test-story-" + Guid.NewGuid().ToString("N")[..10];

    // ---------- Article parsing ----------

    [Fact]
    public void Parse_SplitsHeadingsParagraphsAndBullets()
    {
        var blocks = BlogContent.Parse("First line\nsame paragraph.\n\n## 1. Deomali\nTop of Odisha.\n- Carry a jacket\n- Start early\n\nLast.");

        Assert.Collection(blocks,
            b => Assert.Equal(("p", "First line same paragraph."), (b.Type, b.Text)),
            b => Assert.Equal(("h2", "1. Deomali"), (b.Type, b.Text)),
            b => Assert.Equal(("p", "Top of Odisha."), (b.Type, b.Text)),
            b => { Assert.Equal("ul", b.Type); Assert.Equal(["Carry a jacket", "Start early"], b.Items!); },
            b => Assert.Equal(("p", "Last."), (b.Type, b.Text)));
    }

    [Fact]
    public void Parse_NeverPassesHtmlThrough_ItIsJustText()
    {
        var block = Assert.Single(BlogContent.Parse("<script>alert(1)</script>"));
        Assert.Equal("p", block.Type);
        Assert.Equal("<script>alert(1)</script>", block.Text); // Angular and the server renderer both encode it.
    }

    [Fact]
    public void CleanTags_SplitsCommasAndLines_DropsBlanksAndRepeats()
    {
        Assert.Equal(["Koraput", "Deomali trek", "Duduma"], BlogContent.CleanTags(["Koraput, Deomali trek", " ", "koraput\nDuduma"]));
    }

    // ---------- Stories ----------

    [Fact]
    public async Task Create_ThenPublishedStoryIsPublic_WithTagsBlocksAndPublishDate()
    {
        await using var db = TestDb.CreateContext();
        var service = new BlogService(db, new FakeImageStorage());
        var slug = RandomSlug();

        var id = await service.CreateAsync(Story(slug, tags: ["Koraput trip", "koraput TRIP", "Deomali"]));
        try
        {
            var post = await service.GetPublishedBySlugAsync(slug);
            Assert.Equal(["Koraput trip", "Deomali"], post.Tags);
            Assert.Equal(["p", "h2", "p"], post.Blocks.Select(b => b.Type));
            Assert.NotNull(post.PublishedAt);
            Assert.Contains(await service.GetPublishedAsync(100), s => s.BlogPostId == id);
        }
        finally
        {
            await service.DeleteAsync(id);
        }
    }

    [Fact]
    public async Task Draft_IsHiddenFromThePublicSite()
    {
        await using var db = TestDb.CreateContext();
        var service = new BlogService(db, new FakeImageStorage());
        var slug = RandomSlug();

        var id = await service.CreateAsync(Story(slug, published: false));
        try
        {
            await Assert.ThrowsAsync<NotFoundException>(() => service.GetPublishedBySlugAsync(slug));
            Assert.DoesNotContain(await service.GetPublishedAsync(100), s => s.BlogPostId == id);
            Assert.Null((await service.GetAdminPostAsync(id)).PublishedAt);
        }
        finally
        {
            await service.DeleteAsync(id);
        }
    }

    [Fact]
    public async Task DuplicateSlug_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var service = new BlogService(db, new FakeImageStorage());
        var slug = RandomSlug();

        var id = await service.CreateAsync(Story(slug));
        try
        {
            await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Story(slug)));
        }
        finally
        {
            await service.DeleteAsync(id);
        }
    }

    [Fact]
    public async Task Photos_HeroFallsBackToFirstPhoto_DeleteRemovesFiles()
    {
        await using var db = TestDb.CreateContext();
        var storage = new FakeImageStorage();
        var service = new BlogService(db, storage);
        var slug = RandomSlug();

        var id = await service.CreateAsync(Story(slug));
        var photo = await service.AddPhotoAsync(id, Jpeg(), "  Kunti temple  ");
        Assert.Equal("Kunti temple", photo.Caption);
        Assert.Equal(photo.ImageUrl, (await service.GetPublishedBySlugAsync(slug)).HeroImageUrl);

        await service.SetHeroImageAsync(id, Jpeg());
        var hero = (await service.GetAdminPostAsync(id)).HeroImageUrl;
        Assert.NotEqual(photo.ImageUrl, hero);

        await service.DeleteAsync(id);
        Assert.Contains(photo.ImageUrl, storage.Deleted);
        Assert.Contains(hero!, storage.Deleted);
        Assert.False(await db.BlogPostPhotos.AnyAsync(p => p.BlogPostId == id));
    }

    [Fact]
    public async Task SeededStories_ArePublished()
    {
        await using var db = TestDb.CreateContext();
        var service = new BlogService(db, new FakeImageStorage());

        foreach (var slug in new[] { "best-places-to-visit-in-koraput", "best-places-to-visit-in-mahendragiri" })
        {
            if (!await db.BlogPosts.AnyAsync(p => p.Slug == slug)) continue; // the admin may have removed it
            var post = await service.GetPublishedBySlugAsync(slug);
            Assert.True(post.Tags.Count >= 40);
            Assert.Contains(post.Blocks, b => b.Type == "h2");
        }
    }

    // ---------- Real Travel Moments ----------

    [Fact]
    public async Task Moments_HoldAtMostTen_AndCanBeReorderedAndDeleted()
    {
        await using var db = TestDb.CreateContext();
        var storage = new FakeImageStorage();
        var service = new TravelMomentService(db, storage);
        var added = new List<int>();

        try
        {
            // Fill the gallery to the limit (whatever is already there stays untouched).
            var existing = (await service.GetAllAsync()).Count;
            for (var i = existing; i < ITravelMomentService.MaxPhotos; i++)
            {
                added.Add((await service.AddAsync(Jpeg(), i == existing ? "First test photo" : null)).TravelMomentId);
            }

            var error = await Assert.ThrowsAsync<ValidationAppException>(() => service.AddAsync(Jpeg(), null));
            Assert.Contains("up to 10", string.Join(" ", error.Errors));
            Assert.Equal(ITravelMomentService.MaxPhotos, (await service.GetAllAsync()).Count);

            if (added.Count >= 2)
            {
                var a = added[^2];
                var b = added[^1];
                await service.MoveAsync(b, -1);
                var order = (await service.GetAllAsync()).Select(m => m.TravelMomentId).ToList();
                Assert.True(order.IndexOf(b) < order.IndexOf(a));
            }

            if (added.Count > 0)
            {
                await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateCaptionAsync(added[0], new string('x', 151)));
            }
        }
        finally
        {
            foreach (var id in added) await service.DeleteAsync(id);
        }

        Assert.All(added, id => Assert.DoesNotContain(id, db.TravelMoments.Select(m => m.TravelMomentId)));
        Assert.Equal(added.Count, storage.Deleted.Count);
    }

    [Fact]
    public async Task Moments_RejectNonImages()
    {
        await using var db = TestDb.CreateContext();
        var service = new TravelMomentService(db, new FakeImageStorage());

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.AddAsync(new UploadedImage(new MemoryStream([1, 2, 3]), "doc.pdf", "application/pdf", 3), null));
    }
}
