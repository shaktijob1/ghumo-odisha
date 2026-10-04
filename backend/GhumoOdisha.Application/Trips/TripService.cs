using System.Data;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Seo;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Trips;

public class TripService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : ITripService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private const long MaxItineraryPdfSizeBytes = 10 * 1024 * 1024;

    // ---------- Public ----------

    public async Task<PagedResult<TripSummaryDto>> GetActiveTripsAsync(int page, int pageSize, string? search = null, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default, string? destination = null, bool upcomingOnly = false)
    {
        var query = db.Trips
            .Where(t => t.Status == TripStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Title, destinations, highlights or places covered ("jirang" finds the Mahendragiri trip),
            // forgiving small misspellings. Matched in memory: there are only ever tens of active trips.
            var candidates = await db.Trips.AsNoTracking()
                .Where(t => t.Status == TripStatus.Active)
                .Select(t => new
                {
                    t.TripId,
                    t.Title,
                    t.PlacesCovered,
                    Destinations = t.Destinations.Select(d => d.Name).ToList(),
                    Highlights = t.TripHighlights.Select(h => h.PlaceName).ToList(),
                })
                .ToListAsync(cancellationToken);
            var matchingIds = candidates
                .Where(t => TripSearch.Matches(search, new[] { t.Title }
                    .Concat(t.Destinations).Concat(t.Highlights).Concat(TripSearch.ParsePlaces(t.PlacesCovered))))
                .Select(t => t.TripId)
                .ToList();
            query = query.Where(t => matchingIds.Contains(t.TripId));
        }

        if (!string.IsNullOrWhiteSpace(destination))
        {
            query = query.Where(t => t.Destinations.Any(d => d.Name == destination));
        }

        if (fromDate.HasValue || toDate.HasValue)
        {
            query = query.Where(t => t.TripDateSlots.Any(s =>
                s.Status == TripDateSlotStatus.Active &&
                (!fromDate.HasValue || s.EndDate >= fromDate.Value) &&
                (!toDate.HasValue || s.StartDate <= toDate.Value)));
        }

        // Only trips a visitor can still book: an active departure from today on (inside the date
        // range, if one is given). Lets the home page page through trips with an accurate total.
        if (upcomingOnly)
        {
            var fromToday = TripCalendar.Today();
            query = query.Where(t => t.TripDateSlots.Any(s =>
                s.Status == TripDateSlotStatus.Active && s.StartDate >= fromToday &&
                (!fromDate.HasValue || s.EndDate >= fromDate.Value) &&
                (!toDate.HasValue || s.StartDate <= toDate.Value)));
        }

        // Soonest departure first (the same "next departure" the cards show — see MapToSummary);
        // trips with no upcoming date go last. Title only breaks ties. Ordering alphabetically hid
        // near-term trips behind others once there were more trips than fit on one page.
        var today = TripCalendar.Today();
        query = query
            .OrderBy(t => t.TripDateSlots
                .Where(s => s.Status == TripDateSlotStatus.Active && s.StartDate >= today)
                .Min(s => (DateOnly?)s.StartDate) ?? DateOnly.MaxValue)
            .ThenBy(t => t.Title)
            .ThenBy(t => t.TripId);

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

        var items = trips.Select(t => MapToSummary(t, fromDate, toDate)).ToList();

        return new PagedResult<TripSummaryDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// Every place that has a bookable trip (an active trip with an active departure from today on,
    /// inside the date range if one is given) — the home page's location dropdown, which pages
    /// through trips and so can't collect the places from the cards it has loaded.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetUpcomingTripLocationsAsync(DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
    {
        var fromToday = TripCalendar.Today();
        return await db.Trips
            .Where(t => t.Status == TripStatus.Active && t.TripDateSlots.Any(s =>
                s.Status == TripDateSlotStatus.Active && s.StartDate >= fromToday &&
                (!fromDate.HasValue || s.EndDate >= fromDate.Value) &&
                (!toDate.HasValue || s.StartDate <= toDate.Value)))
            .SelectMany(t => t.Destinations.Select(d => d.Name))
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetUpcomingTripPlacesAsync(CancellationToken cancellationToken = default)
    {
        var fromToday = TripCalendar.Today();
        var stored = await db.Trips.AsNoTracking()
            .Where(t => t.Status == TripStatus.Active && t.PlacesCovered != null && t.TripDateSlots.Any(s =>
                s.Status == TripDateSlotStatus.Active && s.StartDate >= fromToday))
            .Select(t => t.PlacesCovered)
            .ToListAsync(cancellationToken);
        return stored.SelectMany(TripSearch.ParsePlaces)
            .DistinctBy(p => p.ToLowerInvariant())
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<TripDetailDto> GetTripDetailAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await LoadFullTripAsync(tripId, cancellationToken);

        if (trip is null || trip.Status != TripStatus.Active)
        {
            throw new NotFoundException("Trip not found.");
        }

        return MapToDetail(trip, await GenderCountsAsync(trip, cancellationToken));
    }

    public async Task<IReadOnlyList<DateSlotDto>> GetActiveDateSlotsAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var today = TripCalendar.Today();
        var slots = await db.TripDateSlots
            .Where(s => s.TripId == tripId && s.Status == TripDateSlotStatus.Active && s.StartDate >= today)
            .OrderBy(s => s.StartDate)
            .ToListAsync(cancellationToken);

        var counts = await SlotGenderRules.BookedAsync(db, slots.Select(s => s.TripDateSlotId).ToList(), cancellationToken: cancellationToken);
        return slots.Select(s => MapToDateSlot(s, counts)).ToList();
    }

    // ---------- Admin: trips ----------

    public async Task<PagedResult<AdminTripListItemDto>> GetAdminTripsAsync(int page, int pageSize, string? search, TripStatus? status, CancellationToken cancellationToken = default)
    {
        var query = db.Trips.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.Title.Contains(search));
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        query = query.OrderBy(t => t.Title);

        var totalCount = await query.CountAsync(cancellationToken);

        var trips = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.TripDateSlots)
            .ToListAsync(cancellationToken);

        var tripIds = trips.Select(t => t.TripId).ToList();
        var confirmedCounts = await db.Bookings
            .Where(b => tripIds.Contains(b.TripId) && b.BookingStatus == BookingStatus.Confirmed)
            .GroupBy(b => b.TripId)
            .Select(g => new { TripId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TripId, g => g.Count, cancellationToken);

        var items = trips.Select(t => MapToAdminListItem(t, confirmedCounts.GetValueOrDefault(t.TripId))).ToList();

        return new PagedResult<AdminTripListItemDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<AdminTripDetailDto> GetAdminTripDetailAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await LoadFullTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        return MapToAdminDetail(trip, await GenderCountsAsync(trip, cancellationToken));
    }

    public async Task<int> CreateTripAsync(CreateTripRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Title = request.Title,
            Description = request.Description,
            AmountPerPerson = request.AmountPerPerson,
            IncludesBreakfast = request.IncludesBreakfast,
            IncludesLunch = request.IncludesLunch,
            IncludesDinner = request.IncludesDinner,
            IncludesStay = request.IncludesStay,
            IncludesCoordinator = request.IncludesCoordinator,
            IncludesAcVehicle = request.IncludesAcVehicle,
            IncludesPushbackVehicle = request.IncludesPushbackVehicle,
            IncludesCamping = request.IncludesCamping,
            IncludesBonfire = request.IncludesBonfire,
            IncludesMusicalNight = request.IncludesMusicalNight,
            IncludesSwimmingPool = request.IncludesSwimmingPool,
            AllowCoupons = request.AllowCoupons,
            PlacesCovered = TripSearch.JoinPlaces(request.PlacesCovered),
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (request.DestinationIds is { Count: > 0 })
        {
            var destinations = await db.Destinations
                .Where(d => request.DestinationIds.Contains(d.DestinationId))
                .ToListAsync(cancellationToken);
            foreach (var destination in destinations)
            {
                trip.Destinations.Add(destination);
            }
        }

        db.Trips.Add(trip);
        await db.SaveChangesAsync(cancellationToken);
        return trip.TripId;
    }

    public async Task UpdateTripAsync(int tripId, UpdateTripRequest request, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.Include(t => t.Destinations).FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        trip.Title = request.Title;
        trip.Description = request.Description;
        trip.AmountPerPerson = request.AmountPerPerson;
        trip.IncludesBreakfast = request.IncludesBreakfast;
        trip.IncludesLunch = request.IncludesLunch;
        trip.IncludesDinner = request.IncludesDinner;
        trip.IncludesStay = request.IncludesStay;
        trip.IncludesCoordinator = request.IncludesCoordinator;
        trip.IncludesAcVehicle = request.IncludesAcVehicle;
        trip.IncludesPushbackVehicle = request.IncludesPushbackVehicle;
        trip.IncludesCamping = request.IncludesCamping;
        trip.IncludesBonfire = request.IncludesBonfire;
        trip.IncludesMusicalNight = request.IncludesMusicalNight;
        trip.IncludesSwimmingPool = request.IncludesSwimmingPool;
        trip.AllowCoupons = request.AllowCoupons;
        // Left alone when an older client doesn't send the list at all.
        if (request.PlacesCovered is not null) trip.PlacesCovered = TripSearch.JoinPlaces(request.PlacesCovered);
        trip.Status = request.Status;
        trip.UpdatedAt = DateTime.UtcNow;

        var desiredIds = (request.DestinationIds ?? []).ToHashSet();
        var currentIds = trip.Destinations.Select(d => d.DestinationId).ToHashSet();

        foreach (var destination in trip.Destinations.Where(d => !desiredIds.Contains(d.DestinationId)).ToList())
        {
            trip.Destinations.Remove(destination);
        }

        var idsToAdd = desiredIds.Except(currentIds).ToList();
        if (idsToAdd.Count > 0)
        {
            var destinationsToAdd = await db.Destinations
                .Where(d => idsToAdd.Contains(d.DestinationId))
                .ToListAsync(cancellationToken);
            foreach (var destination in destinationsToAdd)
            {
                trip.Destinations.Add(destination);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTripAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        var hasConfirmedBookings = await db.Bookings
            .AnyAsync(b => b.TripId == tripId && b.BookingStatus == BookingStatus.Confirmed, cancellationToken);

        if (hasConfirmedBookings)
        {
            throw new ConflictException("Cannot delete a trip with confirmed bookings.");
        }

        trip.Status = TripStatus.Inactive;
        trip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin: trip photos ----------

    public async Task<int> AddTripPhotoAsync(int tripId, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        ValidateImage(image);

        var nextOrder = await NextDisplayOrderAsync(db.TripPhotos.Where(p => p.TripId == tripId), cancellationToken);
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "trips", cancellationToken);

        var photo = new TripPhoto { TripId = tripId, ImageUrl = url, DisplayOrder = nextOrder, CreatedAt = DateTime.UtcNow };
        db.TripPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        return photo.TripPhotoId;
    }

    public async Task DeleteTripPhotoAsync(int tripPhotoId, CancellationToken cancellationToken = default)
    {
        var photo = await db.TripPhotos.FirstOrDefaultAsync(p => p.TripPhotoId == tripPhotoId, cancellationToken)
            ?? throw new NotFoundException("Trip photo not found.");

        db.TripPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(photo.ImageUrl);
    }

    public async Task UpdateTripPhotoOrderAsync(int tripPhotoId, int displayOrder, CancellationToken cancellationToken = default)
    {
        var photo = await db.TripPhotos.FirstOrDefaultAsync(p => p.TripPhotoId == tripPhotoId, cancellationToken)
            ?? throw new NotFoundException("Trip photo not found.");

        photo.DisplayOrder = displayOrder;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin: highlights ----------

    public async Task<int> AddTripHighlightAsync(int tripId, AddTripHighlightRequest request, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        ValidateImage(image);

        var nextOrder = await NextDisplayOrderAsync(db.TripHighlights.Where(h => h.TripId == tripId), cancellationToken);
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "highlights", cancellationToken);

        var highlight = new TripHighlight
        {
            TripId = tripId,
            PlaceName = request.PlaceName,
            Description = request.Description,
            PhotoUrl = url,
            DisplayOrder = nextOrder,
            CreatedAt = DateTime.UtcNow
        };

        db.TripHighlights.Add(highlight);
        await db.SaveChangesAsync(cancellationToken);
        return highlight.TripHighlightId;
    }

    public async Task UpdateTripHighlightAsync(int highlightId, UpdateTripHighlightRequest request, UploadedImage? image, CancellationToken cancellationToken = default)
    {
        var highlight = await db.TripHighlights.FirstOrDefaultAsync(h => h.TripHighlightId == highlightId, cancellationToken)
            ?? throw new NotFoundException("Trip highlight not found.");

        highlight.PlaceName = request.PlaceName;
        highlight.Description = request.Description;
        highlight.DisplayOrder = request.DisplayOrder;

        if (image is not null)
        {
            ValidateImage(image);
            var oldUrl = highlight.PhotoUrl;
            highlight.PhotoUrl = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "highlights", cancellationToken);
            imageStorage.Delete(oldUrl);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTripHighlightAsync(int highlightId, CancellationToken cancellationToken = default)
    {
        var highlight = await db.TripHighlights.FirstOrDefaultAsync(h => h.TripHighlightId == highlightId, cancellationToken)
            ?? throw new NotFoundException("Trip highlight not found.");

        db.TripHighlights.Remove(highlight);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(highlight.PhotoUrl);
    }

    // ---------- Admin: date slots ----------

    public async Task<int> AddDateSlotAsync(int tripId, AddDateSlotRequest request, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        var now = DateTime.UtcNow;
        var slot = new TripDateSlot
        {
            TripId = tripId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalSeats = request.TotalSeats,
            AvailableSeats = request.TotalSeats,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.TripDateSlots.Add(slot);
        await db.SaveChangesAsync(cancellationToken);
        return slot.TripDateSlotId;
    }

    public async Task UpdateDateSlotAsync(int dateSlotId, UpdateDateSlotRequest request, CancellationToken cancellationToken = default)
    {
        // Same locked pattern as confirm / cancel: hold the slot row so a confirm can't deduct seats
        // between reading the seat counts and writing the new ones.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var slot = await db.TripDateSlots
                .FromSqlInterpolated($"SELECT * FROM TripDateSlots WHERE TripDateSlotId = {dateSlotId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Date slot not found.");

            // Seats already taken (confirmed and completed bookings) — exactly what confirm deducted and cancel hasn't restored.
            var bookedSeats = slot.TotalSeats - slot.AvailableSeats;
            if (request.TotalSeats < bookedSeats)
            {
                throw new ConflictException($"Cannot reduce total seats below the {bookedSeats} already booked on this date.");
            }

            slot.StartDate = request.StartDate;
            slot.EndDate = request.EndDate;
            slot.TotalSeats = request.TotalSeats;
            slot.AvailableSeats = request.TotalSeats - bookedSeats;
            slot.Status = request.Status;
            slot.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task DeleteDateSlotAsync(int dateSlotId, CancellationToken cancellationToken = default)
    {
        var slot = await db.TripDateSlots.FirstOrDefaultAsync(s => s.TripDateSlotId == dateSlotId, cancellationToken)
            ?? throw new NotFoundException("Date slot not found.");

        var bookings = await db.Bookings.Where(b => b.TripDateSlotId == dateSlotId).ToListAsync(cancellationToken);

        // Anything that was ever confirmed took seats and money, so it keeps the departure alive.
        var confirmed = bookings.Count(b => b.ConfirmedAt != null || b.BookingStatus is BookingStatus.Confirmed or BookingStatus.Completed);
        if (confirmed > 0)
        {
            throw new ConflictException($"Cannot delete this date — it has {confirmed} confirmed (or paid-then-cancelled) booking(s).");
        }

        // What's left was never paid (an unpaid checkout, expired, rejected, withdrawn): no seats were
        // taken and nothing was paid. Unlink them, keeping the dates so their history still reads right.
        var now = DateTime.UtcNow;
        foreach (var booking in bookings)
        {
            booking.RemovedSlotStartDate = slot.StartDate;
            booking.RemovedSlotEndDate = slot.EndDate;
            booking.TripDateSlotId = null;
            booking.UpdatedAt = now;
            // Close a checkout still open, so a payment arriving for it later is refunded automatically.
            if (booking.BookingStatus is BookingStatus.AwaitingPayment or BookingStatus.Pending)
            {
                booking.BookingStatus = BookingStatus.Cancelled;
                booking.CancellationReason = "Departure date removed before payment";
                booking.CancelledAt = now;
            }
        }

        db.TripDateSlots.Remove(slot);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin: itinerary ----------

    public async Task<int> AddItineraryDayAsync(int tripId, AddItineraryDayRequest request, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        await EnsureDayNumberFreeAsync(tripId, request.DayNumber, exceptDayId: null, cancellationToken);
        var nextOrder = await NextDisplayOrderAsync(db.ItineraryDays.Where(d => d.TripId == tripId), cancellationToken);

        var day = new ItineraryDay
        {
            TripId = tripId,
            DayNumber = request.DayNumber,
            Title = request.Title,
            Description = request.Description,
            DisplayOrder = nextOrder
        };

        db.ItineraryDays.Add(day);
        await db.SaveChangesAsync(cancellationToken);
        return day.ItineraryDayId;
    }

    public async Task UpdateItineraryDayAsync(int dayId, UpdateItineraryDayRequest request, CancellationToken cancellationToken = default)
    {
        var day = await db.ItineraryDays.FirstOrDefaultAsync(d => d.ItineraryDayId == dayId, cancellationToken)
            ?? throw new NotFoundException("Itinerary day not found.");

        await EnsureDayNumberFreeAsync(day.TripId, request.DayNumber, exceptDayId: dayId, cancellationToken);
        day.DayNumber = request.DayNumber;
        day.Title = request.Title;
        day.Description = request.Description;
        day.DisplayOrder = request.DisplayOrder;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDayNumberFreeAsync(int tripId, int dayNumber, int? exceptDayId, CancellationToken cancellationToken)
    {
        var taken = await db.ItineraryDays.AnyAsync(
            d => d.TripId == tripId && d.DayNumber == dayNumber && (exceptDayId == null || d.ItineraryDayId != exceptDayId),
            cancellationToken);
        if (taken)
        {
            throw new ConflictException($"Day {dayNumber} already exists in this itinerary. Edit that day or pick another number.");
        }
    }

    public async Task DeleteItineraryDayAsync(int dayId, CancellationToken cancellationToken = default)
    {
        var day = await db.ItineraryDays.FirstOrDefaultAsync(d => d.ItineraryDayId == dayId, cancellationToken)
            ?? throw new NotFoundException("Itinerary day not found.");

        db.ItineraryDays.Remove(day);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> AddItineraryPointAsync(int dayId, AddItineraryPointRequest request, CancellationToken cancellationToken = default)
    {
        var dayExists = await db.ItineraryDays.AnyAsync(d => d.ItineraryDayId == dayId, cancellationToken);
        if (!dayExists)
        {
            throw new NotFoundException("Itinerary day not found.");
        }

        var nextOrder = await NextDisplayOrderAsync(db.ItineraryPoints.Where(p => p.ItineraryDayId == dayId), cancellationToken);

        var point = new ItineraryPoint
        {
            ItineraryDayId = dayId,
            Time = request.Time,
            Description = request.Description,
            DisplayOrder = nextOrder
        };

        db.ItineraryPoints.Add(point);
        await db.SaveChangesAsync(cancellationToken);
        return point.ItineraryPointId;
    }

    public async Task UpdateItineraryPointAsync(int pointId, UpdateItineraryPointRequest request, CancellationToken cancellationToken = default)
    {
        var point = await db.ItineraryPoints.FirstOrDefaultAsync(p => p.ItineraryPointId == pointId, cancellationToken)
            ?? throw new NotFoundException("Itinerary point not found.");

        point.Time = request.Time;
        point.Description = request.Description;
        point.DisplayOrder = request.DisplayOrder;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteItineraryPointAsync(int pointId, CancellationToken cancellationToken = default)
    {
        var point = await db.ItineraryPoints.FirstOrDefaultAsync(p => p.ItineraryPointId == pointId, cancellationToken)
            ?? throw new NotFoundException("Itinerary point not found.");

        db.ItineraryPoints.Remove(point);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin: room photos ----------

    public async Task<int> AddRoomPhotoAsync(int tripId, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        ValidateImage(image);

        var nextOrder = await NextDisplayOrderAsync(db.RoomPhotos.Where(p => p.TripId == tripId), cancellationToken);
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "rooms", cancellationToken);

        var photo = new RoomPhoto { TripId = tripId, ImageUrl = url, DisplayOrder = nextOrder, CreatedAt = DateTime.UtcNow };
        db.RoomPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        return photo.RoomPhotoId;
    }

    public async Task DeleteRoomPhotoAsync(int roomPhotoId, CancellationToken cancellationToken = default)
    {
        var photo = await db.RoomPhotos.FirstOrDefaultAsync(p => p.RoomPhotoId == roomPhotoId, cancellationToken)
            ?? throw new NotFoundException("Room photo not found.");

        db.RoomPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(photo.ImageUrl);
    }

    public async Task UpdateRoomPhotoOrderAsync(int roomPhotoId, int displayOrder, CancellationToken cancellationToken = default)
    {
        var photo = await db.RoomPhotos.FirstOrDefaultAsync(p => p.RoomPhotoId == roomPhotoId, cancellationToken)
            ?? throw new NotFoundException("Room photo not found.");

        photo.DisplayOrder = displayOrder;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin: vehicle photos ----------

    public async Task<int> AddVehiclePhotoAsync(int tripId, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        ValidateImage(image);

        var nextOrder = await NextDisplayOrderAsync(db.VehiclePhotos.Where(p => p.TripId == tripId), cancellationToken);
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "vehicles", cancellationToken);

        var photo = new VehiclePhoto { TripId = tripId, ImageUrl = url, DisplayOrder = nextOrder, CreatedAt = DateTime.UtcNow };
        db.VehiclePhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        return photo.VehiclePhotoId;
    }

    public async Task DeleteVehiclePhotoAsync(int vehiclePhotoId, CancellationToken cancellationToken = default)
    {
        var photo = await db.VehiclePhotos.FirstOrDefaultAsync(p => p.VehiclePhotoId == vehiclePhotoId, cancellationToken)
            ?? throw new NotFoundException("Vehicle photo not found.");

        db.VehiclePhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(photo.ImageUrl);
    }

    public async Task UpdateVehiclePhotoOrderAsync(int vehiclePhotoId, int displayOrder, CancellationToken cancellationToken = default)
    {
        var photo = await db.VehiclePhotos.FirstOrDefaultAsync(p => p.VehiclePhotoId == vehiclePhotoId, cancellationToken)
            ?? throw new NotFoundException("Vehicle photo not found.");

        photo.DisplayOrder = displayOrder;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Admin: itinerary PDF ----------

    public async Task UploadItineraryPdfAsync(int tripId, UploadedImage pdf, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        ValidateItineraryPdf(pdf);

        var oldUrl = trip.ItineraryPdfUrl;
        trip.ItineraryPdfUrl = await imageStorage.SaveAsync(pdf.Content, pdf.FileName, pdf.ContentType, "itineraries", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldUrl))
        {
            imageStorage.Delete(oldUrl);
        }
    }

    public async Task DeleteItineraryPdfAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        var url = trip.ItineraryPdfUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        trip.ItineraryPdfUrl = null;
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(url);
    }

    private static void ValidateItineraryPdf(UploadedImage pdf)
    {
        var errors = new List<string>();

        if (!string.Equals(pdf.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Only PDF files are allowed.");
        }

        if (pdf.Length <= 0 || pdf.Length > MaxItineraryPdfSizeBytes)
        {
            errors.Add("Itinerary PDF must be no larger than 10 MB.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }

    // ---------- Admin: pickup points ----------

    public async Task<int> AddPickupPointAsync(int tripId, AddPickupPointRequest request, CancellationToken cancellationToken = default)
    {
        var tripExists = await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken);
        if (!tripExists)
        {
            throw new NotFoundException("Trip not found.");
        }

        var nextOrder = await NextDisplayOrderAsync(db.PickupPoints.Where(p => p.TripId == tripId), cancellationToken);

        var point = new PickupPoint
        {
            TripId = tripId,
            Location = request.Location,
            Time = request.Time,
            DisplayOrder = nextOrder,
            CreatedAt = DateTime.UtcNow
        };

        db.PickupPoints.Add(point);
        await db.SaveChangesAsync(cancellationToken);
        return point.PickupPointId;
    }

    public async Task UpdatePickupPointAsync(int pickupPointId, UpdatePickupPointRequest request, CancellationToken cancellationToken = default)
    {
        var point = await db.PickupPoints.FirstOrDefaultAsync(p => p.PickupPointId == pickupPointId, cancellationToken)
            ?? throw new NotFoundException("Pickup point not found.");

        point.Location = request.Location;
        point.Time = request.Time;
        point.DisplayOrder = request.DisplayOrder;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePickupPointAsync(int pickupPointId, CancellationToken cancellationToken = default)
    {
        var point = await db.PickupPoints.FirstOrDefaultAsync(p => p.PickupPointId == pickupPointId, cancellationToken)
            ?? throw new NotFoundException("Pickup point not found.");

        db.PickupPoints.Remove(point);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Helpers ----------

    private Task<Trip?> LoadFullTripAsync(int tripId, CancellationToken cancellationToken) =>
        db.Trips
            .Include(t => t.TripPhotos)
            .Include(t => t.TripHighlights)
            .Include(t => t.RoomPhotos)
            .Include(t => t.VehiclePhotos)
            .Include(t => t.PickupPoints)
            .Include(t => t.TripDateSlots)
            .Include(t => t.ItineraryDays)
                .ThenInclude(d => d.ItineraryPoints)
            .Include(t => t.Destinations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken);

    private static async Task<int> NextDisplayOrderAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        where T : class
    {
        var count = await query.CountAsync(cancellationToken);
        return count;
    }

    private void ValidateImage(UploadedImage image)
    {
        var errors = new List<string>();

        if (!AllowedImageContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only JPG, PNG or WebP images are allowed.");
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

    internal static TripSummaryDto MapToSummary(Trip trip) => MapToSummary(trip, null, null);

    /// <summary>
    /// With a date range (the home page's month search), the card's next departure and departure
    /// dates come from that range only — searching November shows the November departures, not
    /// whatever comes next from today.
    /// </summary>
    internal static TripSummaryDto MapToSummary(Trip trip, DateOnly? fromDate, DateOnly? toDate)
    {
        var coverImage = trip.TripPhotos.OrderBy(p => p.DisplayOrder).FirstOrDefault()?.ImageUrl;
        var upcoming = UpcomingSlots(trip, fromDate, toDate);
        var nextSlot = upcoming.FirstOrDefault();
        var onCard = upcoming.Take(MaxUpcomingSlotsOnCard).ToList();
        var nextOpen = UpcomingSlots(trip, null, null)
            .Where(s => !onCard.Contains(s) && s.AvailableSeats > 0 && !SlotBookingRules.IsClosedOnline(s.StartDate))
            .Take(MaxNextOpenSlotsOnCard)
            .Select(s => new UpcomingSlotDto(s.StartDate, s.AvailableSeats))
            .ToList();

        return new TripSummaryDto(
            trip.TripId,
            trip.Title,
            trip.AmountPerPerson,
            coverImage,
            nextSlot is null ? null : FormatDuration(nextSlot.StartDate, nextSlot.EndDate),
            nextSlot?.StartDate,
            nextSlot?.EndDate,
            nextSlot is null ? null : SlotBookingRules.IsClosedOnline(nextSlot.StartDate) ? 0 : nextSlot.AvailableSeats,
            nextSlot?.TotalSeats,
            MapInclusions(trip),
            trip.TripHighlights.OrderBy(h => h.DisplayOrder).Select(h => h.PlaceName).ToList(),
            MapPhotos(trip),
            trip.Destinations.OrderBy(d => d.Name).Select(d => d.Name).ToList(),
            // Dates inside the online cutoff read as full on the card, like a sold-out date.
            onCard.Select(s => new UpcomingSlotDto(s.StartDate, SlotBookingRules.IsClosedOnline(s.StartDate) ? 0 : s.AvailableSeats)).ToList(),
            nextOpen);
    }

    // Feeds the trip card's departure dates — information only, never used for booking.
    private const int MaxUpcomingSlotsOnCard = 8;
    private const int MaxNextOpenSlotsOnCard = 3;

    /// <summary>Active departures from today on, soonest first; with a range, only those overlapping it
    /// (the same overlap rule the month search uses to pick which trips to list).</summary>
    private static List<TripDateSlot> UpcomingSlots(Trip trip, DateOnly? fromDate, DateOnly? toDate)
    {
        var today = TripCalendar.Today();
        return trip.TripDateSlots
            .Where(s => s.Status == TripDateSlotStatus.Active && s.StartDate >= today
                && (!fromDate.HasValue || s.EndDate >= fromDate.Value)
                && (!toDate.HasValue || s.StartDate <= toDate.Value))
            .OrderBy(s => s.StartDate)
            .ToList();
    }

    private static AdminTripListItemDto MapToAdminListItem(Trip trip, int confirmedBookingCount)
    {
        var nextSlot = trip.TripDateSlots.OrderBy(s => s.StartDate).FirstOrDefault();

        return new AdminTripListItemDto(
            trip.TripId,
            trip.Title,
            trip.AmountPerPerson,
            trip.Status,
            trip.TripDateSlots.Count,
            nextSlot?.StartDate,
            nextSlot?.TotalSeats,
            nextSlot?.AvailableSeats,
            confirmedBookingCount);
    }

    private static TripDetailDto MapToDetail(Trip trip, Dictionary<int, SlotGenderCount> genders)
    {
        var inclusions = MapInclusions(trip);
        var itinerary = MapItineraryDays(trip);
        var pickups = MapPickupPoints(trip);
        // Departed dates are never offered to customers — not just hidden by the UI.
        var slots = trip.TripDateSlots.Where(s => s.Status == TripDateSlotStatus.Active && s.StartDate >= TripCalendar.Today())
            .OrderBy(s => s.StartDate).Select(s => MapToDateSlot(s, genders)).ToList();

        var departureCity = TripPageContent.DetectDepartureCity(TripPageContent.HomeCity,
            itinerary.SelectMany(d => new[] { d.Title, d.Description }.Concat(d.Points.Select(p => p.Description)))
                .Concat(pickups.Select(p => p.Location)));
        var next = slots.FirstOrDefault();
        var duration = next is null ? null : TripPageContent.DurationLabel(next.StartDate, next.EndDate);

        return new TripDetailDto(
            trip.TripId,
            trip.Title,
            trip.Description,
            trip.AmountPerPerson,
            inclusions,
            MapPhotos(trip),
            MapHighlights(trip),
            itinerary,
            MapRoomPhotos(trip),
            MapVehiclePhotos(trip),
            pickups,
            slots,
            trip.ItineraryPdfUrl,
            trip.Destinations.Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new TripDestinationLink(d.Name, d.Slug)).ToList(),
            departureCity,
            duration,
            TripSearch.ParsePlaces(trip.PlacesCovered),
            TripPageContent.BuildTripFaqs(trip.Title, departureCity, pickups.Select(p => (p.Location, p.Time)).ToList(),
                TripPageContent.IncludedItems(inclusions), duration, slots.Count > 0));
    }

    private static AdminTripDetailDto MapToAdminDetail(Trip trip, Dictionary<int, SlotGenderCount> genders) => new(
        trip.TripId,
        trip.Title,
        trip.Description,
        trip.AmountPerPerson,
        MapInclusions(trip),
        trip.Status,
        trip.CreatedAt,
        trip.UpdatedAt,
        MapPhotos(trip),
        MapHighlights(trip),
        MapItineraryDays(trip),
        MapRoomPhotos(trip),
        MapVehiclePhotos(trip),
        MapPickupPoints(trip),
        trip.TripDateSlots.OrderBy(s => s.StartDate).Select(s => MapToDateSlot(s, genders)).ToList(),
        trip.Destinations.Select(d => d.DestinationId).ToList(),
        trip.Destinations.Select(d => d.Name).ToList(),
        trip.ItineraryPdfUrl,
        trip.AllowCoupons,
        TripSearch.ParsePlaces(trip.PlacesCovered));

    private static TripInclusionsDto MapInclusions(Trip trip) => new(
        trip.IncludesBreakfast, trip.IncludesLunch, trip.IncludesDinner, trip.IncludesStay, trip.IncludesCoordinator,
        trip.IncludesAcVehicle, trip.IncludesPushbackVehicle, trip.IncludesCamping, trip.IncludesBonfire,
        trip.IncludesMusicalNight, trip.IncludesSwimmingPool);

    private static List<TripPhotoDto> MapPhotos(Trip trip) => trip.TripPhotos
        .OrderBy(p => p.DisplayOrder)
        .Select(p => new TripPhotoDto(p.TripPhotoId, p.ImageUrl, p.DisplayOrder))
        .ToList();

    private static List<RoomPhotoDto> MapRoomPhotos(Trip trip) => trip.RoomPhotos
        .OrderBy(p => p.DisplayOrder)
        .Select(p => new RoomPhotoDto(p.RoomPhotoId, p.ImageUrl, p.DisplayOrder))
        .ToList();

    private static List<VehiclePhotoDto> MapVehiclePhotos(Trip trip) => trip.VehiclePhotos
        .OrderBy(p => p.DisplayOrder)
        .Select(p => new VehiclePhotoDto(p.VehiclePhotoId, p.ImageUrl, p.DisplayOrder))
        .ToList();

    private static List<PickupPointDto> MapPickupPoints(Trip trip) => trip.PickupPoints
        .OrderBy(p => p.DisplayOrder)
        .Select(p => new PickupPointDto(p.PickupPointId, p.Location, p.Time, p.DisplayOrder))
        .ToList();

    private static List<TripHighlightDto> MapHighlights(Trip trip) => trip.TripHighlights
        .OrderBy(h => h.DisplayOrder)
        .Select(h => new TripHighlightDto(h.TripHighlightId, h.PlaceName, h.Description, h.PhotoUrl, h.DisplayOrder))
        .ToList();

    // Always Day 1, Day 2, Day 3… regardless of the order days were added in.
    private static List<ItineraryDayDto> MapItineraryDays(Trip trip) => trip.ItineraryDays
        .OrderBy(d => d.DayNumber)
        .ThenBy(d => d.DisplayOrder)
        .Select(d => new ItineraryDayDto(
            d.ItineraryDayId,
            d.DayNumber,
            d.Title,
            d.Description,
            d.DisplayOrder,
            d.ItineraryPoints.OrderBy(p => p.DisplayOrder).Select(p => new ItineraryPointDto(p.ItineraryPointId, p.Time, p.Description, p.DisplayOrder)).ToList()))
        .ToList();

    private async Task<Dictionary<int, SlotGenderCount>> GenderCountsAsync(Trip trip, CancellationToken cancellationToken) =>
        await SlotGenderRules.BookedAsync(db, trip.TripDateSlots.Select(s => s.TripDateSlotId).ToList(), cancellationToken: cancellationToken);

    private static DateSlotDto MapToDateSlot(TripDateSlot slot, Dictionary<int, SlotGenderCount> genders)
    {
        var booked = genders.GetValueOrDefault(slot.TripDateSlotId);
        // No gender cap: gents and ladies can each take any seat still free.
        var gentsLeft = Math.Max(slot.AvailableSeats, 0);
        var ladiesLeft = gentsLeft;
        return new DateSlotDto(slot.TripDateSlotId, slot.StartDate, slot.EndDate, slot.TotalSeats, slot.AvailableSeats,
            slot.AvailableSeats <= 0 || gentsLeft + ladiesLeft == 0, booked.Gents, booked.Ladies, gentsLeft, ladiesLeft,
            SlotBookingRules.IsClosedOnline(slot.StartDate), slot.Status);
    }

    private static string FormatDuration(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var nights = days - 1;
        var dayLabel = days == 1 ? "Day" : "Days";
        var nightLabel = nights == 1 ? "Night" : "Nights";
        return $"{days} {dayLabel} / {nights} {nightLabel}";
    }
}
