using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Seo;

public interface ISeoService
{
    Task<HomeSeoInfo> GetHomeAsync(CancellationToken cancellationToken = default);
    Task<TripSeoInfo?> GetTripAsync(int tripId, CancellationToken cancellationToken = default);
    Task<DestinationSeoInfo?> GetDestinationAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only public data for search engines and link previews (page titles, descriptions, sitemap,
/// structured data). Only ever exposes what the public trip and destination pages already show.
/// </summary>
public class SeoService(IGhumoOdishaDbContext db) : ISeoService
{
    public async Task<HomeSeoInfo> GetHomeAsync(CancellationToken cancellationToken = default)
    {
        var hero = await db.SiteHeroPhotos.AsNoTracking()
            .Where(h => h.Page == SiteHeroPage.Home)
            .Select(h => h.ImageUrl)
            .FirstOrDefaultAsync(cancellationToken);

        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.Status == TripStatus.Active)
            .OrderBy(t => t.TripId)
            .Select(t => new { t.TripId, t.Title, t.AmountPerPerson })
            .Take(50)
            .ToListAsync(cancellationToken);

        return new HomeSeoInfo(hero, trips.Select(t => new DestinationTripLink(t.Title, SeoSlug.TripPath(t.TripId, t.Title), t.AmountPerPerson)).ToList());
    }

    public async Task<TripSeoInfo?> GetTripAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var today = TripCalendar.Today();
        var trip = await db.Trips.AsNoTracking()
            .Where(t => t.TripId == tripId && t.Status == TripStatus.Active)
            .Select(t => new
            {
                t.TripId,
                t.Title,
                t.Description,
                t.AmountPerPerson,
                Image = t.TripPhotos.OrderBy(p => p.DisplayOrder).Select(p => p.ImageUrl).FirstOrDefault(),
                Destinations = t.Destinations.OrderBy(d => d.Name).Select(d => d.Name).ToList(),
                Departures = t.TripDateSlots
                    .Where(s => s.Status == TripDateSlotStatus.Active && s.StartDate >= today)
                    .OrderBy(s => s.StartDate)
                    .Select(s => new SeoDeparture(s.StartDate, s.EndDate, s.AvailableSeats))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var next = trip.Departures.FirstOrDefault();
        return new TripSeoInfo(
            trip.TripId,
            trip.Title,
            SeoSlug.TripPath(trip.TripId, trip.Title),
            trip.Description,
            trip.AmountPerPerson,
            next is null ? null : DurationLabel(next.StartDate, next.EndDate),
            trip.Image,
            trip.Destinations,
            trip.Departures);
    }

    public async Task<DestinationSeoInfo?> GetDestinationAsync(string slug, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations.AsNoTracking()
            .Where(d => d.Slug == slug && d.IsActive)
            .Select(d => new
            {
                d.Name,
                d.Slug,
                d.Tagline,
                d.AboutText,
                d.KnownFor,
                Image = d.HeroImageUrl ?? d.CoverImageUrl,
                Trips = d.Trips.Where(t => t.Status == TripStatus.Active)
                    .OrderBy(t => t.TripId)
                    .Select(t => new { t.TripId, t.Title, t.AmountPerPerson })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return destination is null
            ? null
            : new DestinationSeoInfo(
                destination.Name,
                destination.Slug,
                destination.Tagline,
                destination.AboutText,
                destination.KnownFor,
                destination.Image,
                destination.Trips.Select(t => new DestinationTripLink(t.Title, SeoSlug.TripPath(t.TripId, t.Title), t.AmountPerPerson)).ToList());
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.Status == TripStatus.Active)
            .OrderBy(t => t.TripId)
            .Select(t => new { t.TripId, t.Title, t.UpdatedAt })
            .ToListAsync(cancellationToken);

        var destinations = await db.Destinations.AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
            .Select(d => new { d.Slug, d.UpdatedAt })
            .ToListAsync(cancellationToken);

        var entries = new List<SitemapEntry>
        {
            new("/", null),
            new("/trips", trips.Count > 0 ? trips.Max(t => t.UpdatedAt) : null),
        };
        entries.AddRange(destinations.Select(d => new SitemapEntry($"/destinations/{d.Slug}", d.UpdatedAt)));
        entries.AddRange(trips.Select(t => new SitemapEntry(SeoSlug.TripPath(t.TripId, t.Title), t.UpdatedAt)));
        entries.Add(new SitemapEntry("/terms", null));
        return entries;
    }

    private static string DurationLabel(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var nights = days - 1;
        return $"{days} {(days == 1 ? "Day" : "Days")} / {nights} {(nights == 1 ? "Night" : "Nights")}";
    }
}
