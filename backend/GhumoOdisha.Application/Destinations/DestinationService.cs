using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Destinations.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Destinations;

public class DestinationService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : IDestinationService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    // ---------- Public ----------

    public async Task<IReadOnlyList<DestinationSummaryDto>> GetActiveDestinationsAsync(CancellationToken cancellationToken = default)
    {
        var destinations = await db.Destinations
            .Where(d => d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Name)
            .Include(d => d.Trips)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return destinations
            .Select(d =>
            {
                var activeTrips = d.Trips.Where(t => t.Status == TripStatus.Active).ToList();
                return new { Destination = d, ActiveTrips = activeTrips };
            })
            .Where(x => x.ActiveTrips.Count > 0)
            .Select(x => new DestinationSummaryDto(
                x.Destination.DestinationId,
                x.Destination.Name,
                x.Destination.Slug,
                x.Destination.Tagline,
                x.Destination.HeroImageUrl,
                x.ActiveTrips.Count,
                x.ActiveTrips.Min(t => (decimal?)t.AmountPerPerson)))
            .ToList();
    }

    public async Task<DestinationDetailDto> GetDestinationBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations
            .Include(d => d.Trips)
            .FirstOrDefaultAsync(d => d.Slug == slug && d.IsActive, cancellationToken)
            ?? throw new NotFoundException("Destination not found.");

        return MapToDetail(destination);
    }

    public async Task<PagedResult<TripSummaryDto>> GetDestinationTripsAsync(string slug, int page, int pageSize, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations.FirstOrDefaultAsync(d => d.Slug == slug && d.IsActive, cancellationToken)
            ?? throw new NotFoundException("Destination not found.");

        var query = db.Trips
            .Where(t => t.Status == TripStatus.Active && t.Destinations.Any(d => d.DestinationId == destination.DestinationId));

        if (fromDate.HasValue || toDate.HasValue)
        {
            query = query.Where(t => t.TripDateSlots.Any(s =>
                s.Status == TripDateSlotStatus.Active &&
                (!fromDate.HasValue || s.EndDate >= fromDate.Value) &&
                (!toDate.HasValue || s.StartDate <= toDate.Value)));
        }

        query = query.OrderBy(t => t.Title);

        var totalCount = await query.CountAsync(cancellationToken);

        var trips = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.TripPhotos)
            .Include(t => t.TripDateSlots)
            .Include(t => t.TripHighlights)
            .Include(t => t.Destinations)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var items = trips.Select(TripService.MapToSummary).ToList();

        return new PagedResult<TripSummaryDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    // ---------- Admin ----------

    public async Task<PagedResult<AdminDestinationListItemDto>> GetAdminDestinationsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        var query = db.Destinations.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d => d.Name.Contains(search));
        }

        query = query.OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name);

        var totalCount = await query.CountAsync(cancellationToken);

        var destinations = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(d => d.Trips)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var items = destinations.Select(MapToAdminListItem).ToList();

        return new PagedResult<AdminDestinationListItemDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<AdminDestinationDetailDto> GetAdminDestinationDetailAsync(int destinationId, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations
            .Include(d => d.Trips)
            .FirstOrDefaultAsync(d => d.DestinationId == destinationId, cancellationToken)
            ?? throw new NotFoundException("Destination not found.");

        return MapToAdminDetail(destination);
    }

    public async Task<IReadOnlyList<AdminDestinationListItemDto>> GetAllForPickerAsync(CancellationToken cancellationToken = default)
    {
        var destinations = await db.Destinations
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.Name)
            .Include(d => d.Trips)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return destinations.Select(MapToAdminListItem).ToList();
    }

    public async Task<int> CreateDestinationAsync(CreateDestinationRequest request, CancellationToken cancellationToken = default)
    {
        var slugTaken = await db.Destinations.AnyAsync(d => d.Slug == request.Slug, cancellationToken);
        if (slugTaken)
        {
            throw new ConflictException("A destination with this slug already exists.");
        }

        var now = DateTime.UtcNow;
        var destination = new Destination
        {
            Name = request.Name,
            Slug = request.Slug,
            Tagline = request.Tagline,
            Region = request.Region,
            AboutText = request.AboutText,
            BestSeason = request.BestSeason,
            DistanceFromBhubaneswar = request.DistanceFromBhubaneswar,
            IdealDuration = request.IdealDuration,
            KnownFor = request.KnownFor,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Destinations.Add(destination);
        await db.SaveChangesAsync(cancellationToken);
        return destination.DestinationId;
    }

    public async Task UpdateDestinationAsync(int destinationId, UpdateDestinationRequest request, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations.FirstOrDefaultAsync(d => d.DestinationId == destinationId, cancellationToken)
            ?? throw new NotFoundException("Destination not found.");

        var slugTaken = await db.Destinations.AnyAsync(d => d.Slug == request.Slug && d.DestinationId != destinationId, cancellationToken);
        if (slugTaken)
        {
            throw new ConflictException("A destination with this slug already exists.");
        }

        destination.Name = request.Name;
        destination.Slug = request.Slug;
        destination.Tagline = request.Tagline;
        destination.Region = request.Region;
        destination.AboutText = request.AboutText;
        destination.BestSeason = request.BestSeason;
        destination.DistanceFromBhubaneswar = request.DistanceFromBhubaneswar;
        destination.IdealDuration = request.IdealDuration;
        destination.KnownFor = request.KnownFor;
        destination.IsActive = request.IsActive;
        destination.DisplayOrder = request.DisplayOrder;
        destination.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDestinationAsync(int destinationId, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations.FirstOrDefaultAsync(d => d.DestinationId == destinationId, cancellationToken)
            ?? throw new NotFoundException("Destination not found.");

        db.Destinations.Remove(destination);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDestinationHeroImageAsync(int destinationId, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var destination = await db.Destinations.FirstOrDefaultAsync(d => d.DestinationId == destinationId, cancellationToken)
            ?? throw new NotFoundException("Destination not found.");

        ValidateImage(image);

        var oldUrl = destination.HeroImageUrl;
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "destinations", cancellationToken);

        destination.HeroImageUrl = url;
        destination.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(oldUrl))
        {
            imageStorage.Delete(oldUrl);
        }
    }

    // ---------- Helpers ----------

    private static DestinationDetailDto MapToDetail(Destination destination) => new(
        destination.DestinationId,
        destination.Name,
        destination.Slug,
        destination.Tagline,
        destination.Region,
        destination.HeroImageUrl,
        destination.AboutText,
        destination.BestSeason,
        destination.DistanceFromBhubaneswar,
        destination.IdealDuration,
        destination.KnownFor,
        destination.Trips.Count(t => t.Status == TripStatus.Active));

    private static AdminDestinationListItemDto MapToAdminListItem(Destination destination) => new(
        destination.DestinationId,
        destination.Name,
        destination.Slug,
        destination.IsActive,
        destination.DisplayOrder,
        destination.HeroImageUrl,
        destination.Trips.Count);

    private static AdminDestinationDetailDto MapToAdminDetail(Destination destination) => new(
        destination.DestinationId,
        destination.Name,
        destination.Slug,
        destination.Tagline,
        destination.Region,
        destination.HeroImageUrl,
        destination.AboutText,
        destination.BestSeason,
        destination.DistanceFromBhubaneswar,
        destination.IdealDuration,
        destination.KnownFor,
        destination.IsActive,
        destination.DisplayOrder,
        destination.CreatedAt,
        destination.UpdatedAt,
        destination.Trips.Select(t => t.TripId).ToList(),
        destination.Trips.Select(t => t.Title).ToList());

    private static void ValidateImage(UploadedImage image)
    {
        var errors = new List<string>();

        if (!AllowedImageContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only JPG or PNG images are allowed.");
        }

        if (image.Length <= 0 || image.Length > MaxImageSizeBytes)
        {
            errors.Add("Image must be no larger than 5 MB.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }
}
