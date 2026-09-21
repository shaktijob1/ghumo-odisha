using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Trips;

public class TripService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : ITripService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    // ---------- Public ----------

    public async Task<PagedResult<TripSummaryDto>> GetActiveTripsAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Trips
            .Where(t => t.Status == TripStatus.Active)
            .OrderBy(t => t.Title);

        var totalCount = await query.CountAsync(cancellationToken);

        var trips = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.TripPhotos)
            .Include(t => t.TripDateSlots)
            .ToListAsync(cancellationToken);

        var items = trips.Select(MapToSummary).ToList();

        return new PagedResult<TripSummaryDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<TripDetailDto> GetTripDetailAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var trip = await LoadFullTripAsync(tripId, cancellationToken);

        if (trip is null || trip.Status != TripStatus.Active)
        {
            throw new NotFoundException("Trip not found.");
        }

        return MapToDetail(trip);
    }

    public async Task<IReadOnlyList<DateSlotDto>> GetActiveDateSlotsAsync(int tripId, CancellationToken cancellationToken = default)
    {
        var slots = await db.TripDateSlots
            .Where(s => s.TripId == tripId && s.Status == TripDateSlotStatus.Active)
            .OrderBy(s => s.StartDate)
            .ToListAsync(cancellationToken);

        return slots.Select(MapToDateSlot).ToList();
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

        return MapToAdminDetail(trip);
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
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Trips.Add(trip);
        await db.SaveChangesAsync(cancellationToken);
        return trip.TripId;
    }

    public async Task UpdateTripAsync(int tripId, UpdateTripRequest request, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.TripId == tripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        trip.Title = request.Title;
        trip.Description = request.Description;
        trip.AmountPerPerson = request.AmountPerPerson;
        trip.IncludesBreakfast = request.IncludesBreakfast;
        trip.IncludesLunch = request.IncludesLunch;
        trip.IncludesDinner = request.IncludesDinner;
        trip.IncludesStay = request.IncludesStay;
        trip.IncludesCoordinator = request.IncludesCoordinator;
        trip.Status = request.Status;
        trip.UpdatedAt = DateTime.UtcNow;

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
        var slot = await db.TripDateSlots.FirstOrDefaultAsync(s => s.TripDateSlotId == dateSlotId, cancellationToken)
            ?? throw new NotFoundException("Date slot not found.");

        var confirmedSeats = await db.Bookings
            .Where(b => b.TripDateSlotId == dateSlotId && b.BookingStatus == BookingStatus.Confirmed)
            .SumAsync(b => (int?)b.NumberOfSeats, cancellationToken) ?? 0;

        if (request.TotalSeats < confirmedSeats)
        {
            throw new ConflictException($"Cannot reduce total seats below the {confirmedSeats} already confirmed on this date.");
        }

        slot.StartDate = request.StartDate;
        slot.EndDate = request.EndDate;
        slot.TotalSeats = request.TotalSeats;
        slot.AvailableSeats = request.TotalSeats - confirmedSeats;
        slot.Status = request.Status;
        slot.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDateSlotAsync(int dateSlotId, CancellationToken cancellationToken = default)
    {
        var slot = await db.TripDateSlots.FirstOrDefaultAsync(s => s.TripDateSlotId == dateSlotId, cancellationToken)
            ?? throw new NotFoundException("Date slot not found.");

        var hasBookings = await db.Bookings.AnyAsync(b => b.TripDateSlotId == dateSlotId, cancellationToken);
        if (hasBookings)
        {
            throw new ConflictException("Cannot delete a date slot that has bookings.");
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

        day.DayNumber = request.DayNumber;
        day.Title = request.Title;
        day.Description = request.Description;
        day.DisplayOrder = request.DisplayOrder;

        await db.SaveChangesAsync(cancellationToken);
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

    private static TripSummaryDto MapToSummary(Trip trip)
    {
        var coverImage = trip.TripPhotos.OrderBy(p => p.DisplayOrder).FirstOrDefault()?.ImageUrl;
        var nextSlot = trip.TripDateSlots
            .Where(s => s.Status == TripDateSlotStatus.Active)
            .OrderBy(s => s.StartDate)
            .FirstOrDefault();

        return new TripSummaryDto(
            trip.TripId,
            trip.Title,
            trip.AmountPerPerson,
            coverImage,
            nextSlot is null ? null : FormatDuration(nextSlot.StartDate, nextSlot.EndDate),
            nextSlot?.StartDate,
            nextSlot?.EndDate,
            nextSlot?.AvailableSeats,
            nextSlot?.TotalSeats,
            MapPhotos(trip));
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

    private static TripDetailDto MapToDetail(Trip trip) => new(
        trip.TripId,
        trip.Title,
        trip.Description,
        trip.AmountPerPerson,
        MapInclusions(trip),
        MapPhotos(trip),
        MapHighlights(trip),
        MapItineraryDays(trip),
        MapRoomPhotos(trip),
        MapVehiclePhotos(trip),
        MapPickupPoints(trip),
        trip.TripDateSlots.Where(s => s.Status == TripDateSlotStatus.Active).OrderBy(s => s.StartDate).Select(MapToDateSlot).ToList());

    private static AdminTripDetailDto MapToAdminDetail(Trip trip) => new(
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
        trip.TripDateSlots.OrderBy(s => s.StartDate).Select(MapToDateSlot).ToList());

    private static TripInclusionsDto MapInclusions(Trip trip) => new(
        trip.IncludesBreakfast, trip.IncludesLunch, trip.IncludesDinner, trip.IncludesStay, trip.IncludesCoordinator);

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

    private static List<ItineraryDayDto> MapItineraryDays(Trip trip) => trip.ItineraryDays
        .OrderBy(d => d.DisplayOrder)
        .Select(d => new ItineraryDayDto(
            d.ItineraryDayId,
            d.DayNumber,
            d.Title,
            d.Description,
            d.DisplayOrder,
            d.ItineraryPoints.OrderBy(p => p.DisplayOrder).Select(p => new ItineraryPointDto(p.ItineraryPointId, p.Time, p.Description, p.DisplayOrder)).ToList()))
        .ToList();

    private static DateSlotDto MapToDateSlot(TripDateSlot slot) => new(
        slot.TripDateSlotId, slot.StartDate, slot.EndDate, slot.TotalSeats, slot.AvailableSeats, slot.AvailableSeats <= 0);

    private static string FormatDuration(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var nights = days - 1;
        var dayLabel = days == 1 ? "Day" : "Days";
        var nightLabel = nights == 1 ? "Night" : "Nights";
        return $"{days} {dayLabel} / {nights} {nightLabel}";
    }
}
