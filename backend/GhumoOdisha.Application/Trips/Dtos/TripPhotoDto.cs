using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

public record TripPhotoDto(int TripPhotoId, string ImageUrl, int DisplayOrder);

public record RoomPhotoDto(int RoomPhotoId, string ImageUrl, int DisplayOrder);

public record VehiclePhotoDto(int VehiclePhotoId, string ImageUrl, int DisplayOrder);

public record PickupPointDto(int PickupPointId, string Location, string Time, int DisplayOrder);

public record TripHighlightDto(int TripHighlightId, string PlaceName, string Description, string PhotoUrl, int DisplayOrder);

public record ItineraryPointDto(int ItineraryPointId, string Time, string Description, int DisplayOrder);

public record ItineraryDayDto(int ItineraryDayId, int DayNumber, string Title, string Description, int DisplayOrder, IReadOnlyList<ItineraryPointDto> Points);

/// <param name="GentsBooked">Gents / ladies already booked (paid) on this date — shown to customers instead of seats left.</param>
/// <param name="GentsLeft">Places still open for gents / ladies — no gender cap, so both equal the seats still free.</param>
public record DateSlotDto(int TripDateSlotId, DateOnly StartDate, DateOnly EndDate, int TotalSeats, int AvailableSeats, bool IsSoldOut,
    int GentsBooked, int LadiesBooked, int GentsLeft, int LadiesLeft,
    /// <summary>Past the online cutoff (6 AM India time on the trip day) or the trip is marked Closed: customers see "Seats filled".</summary>
    bool IsBookingClosed,
    TripDateSlotStatus Status,
    /// <summary>Per-person price for a booking made today (departure price, or early-booking price while it lasts) — what checkout charges.</summary>
    decimal PricePerPerson,
    /// <summary>The departure's normal price (shown struck through while the early price applies).</summary>
    decimal RegularPrice,
    /// <summary>Last day of the early-booking price; null when it doesn't apply today.</summary>
    DateOnly? EarlyBirdEndsOn,
    /// <summary>This departure's own price as set by the admin; null = it uses the trip's price.</summary>
    decimal? SlotPrice = null);

public record TripInclusionsDto(
    bool Breakfast, bool Lunch, bool Dinner, bool Stay, bool Coordinator,
    bool AcVehicle, bool PushbackVehicle, bool Camping, bool Bonfire, bool MusicalNight, bool SwimmingPool);
