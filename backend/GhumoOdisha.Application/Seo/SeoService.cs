using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Seo;

public interface ISeoService
{
    Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default);

    /// <summary>The official /trips/{id}-{slug} path of an active trip, or null when there's no such trip.</summary>
    Task<string?> GetTripPathAsync(int tripId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only public data for the sitemap and URL clean-up. Only ever exposes what the public trip
/// and destination pages already show.
/// </summary>
public class SeoService(IGhumoOdishaDbContext db) : ISeoService
{
    private const int MaxImagesPerPage = 10;

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.Status == TripStatus.Active)
            .OrderBy(t => t.TripId)
            .Select(t => new
            {
                t.TripId,
                t.Title,
                t.UpdatedAt,
                Photos = t.TripPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.ImageUrl).Take(MaxImagesPerPage).ToList(),
            })
            .ToListAsync(cancellationToken);

        var destinations = await db.Destinations.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
            .Select(d => new { d.Slug, d.UpdatedAt, d.HeroImageUrl, d.CoverImageUrl })
            .ToListAsync(cancellationToken);

        DateTime? Latest(IEnumerable<DateTime> dates) => dates.Any() ? dates.Max() : null;
        var tripsUpdated = Latest(trips.Select(t => t.UpdatedAt));
        var destinationsUpdated = Latest(destinations.Select(d => d.UpdatedAt));

        var entries = new List<SitemapEntry>
        {
            new("/", Latest(new[] { tripsUpdated, destinationsUpdated }.Where(d => d.HasValue).Select(d => d!.Value))),
        };
        entries.AddRange(destinations.Select(d => new SitemapEntry(
            $"/destinations/{d.Slug}",
            d.UpdatedAt,
            new[] { d.HeroImageUrl, d.CoverImageUrl }.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().Select(i => i!).ToList())));
        entries.AddRange(trips.Select(t => new SitemapEntry(SeoSlug.TripPath(t.TripId, t.Title), t.UpdatedAt, t.Photos)));
        entries.Add(new SitemapEntry("/terms", null));
        return entries;
    }

    public async Task<string?> GetTripPathAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var title = await db.Trips.AsNoTracking()
            .Where(t => t.TripId == tripId && t.Status == TripStatus.Active)
            .Select(t => t.Title)
            .FirstOrDefaultAsync(cancellationToken);
        return title is null ? null : SeoSlug.TripPath(tripId, title);
    }
}
