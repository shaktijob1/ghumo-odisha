namespace GhumoOdisha.Application.Trips.Dtos;

public record TripPhotoDto(int TripPhotoId, string ImageUrl, int DisplayOrder);

public record RoomPhotoDto(int RoomPhotoId, string ImageUrl, int DisplayOrder);

public record VehiclePhotoDto(int VehiclePhotoId, string ImageUrl, int DisplayOrder);

public record PickupPointDto(int PickupPointId, string Location, string Time, int DisplayOrder);

public record TripHighlightDto(int TripHighlightId, string PlaceName, string Description, string PhotoUrl, int DisplayOrder);

public record ItineraryPointDto(int ItineraryPointId, string Time, string Description, int DisplayOrder);

public record ItineraryDayDto(int ItineraryDayId, int DayNumber, string Title, string Description, int DisplayOrder, IReadOnlyList<ItineraryPointDto> Points);

/// <param name="GentsBooked">Gents / ladies already booked (paid) on this date — shown to customers instead of seats left.</param>
/// <param name="GentsLeft">Places still open for gents / ladies under the 1:1 rule (each side gets half the seats, rounded up).</param>
public record DateSlotDto(int TripDateSlotId, DateOnly StartDate, DateOnly EndDate, int TotalSeats, int AvailableSeats, bool IsSoldOut,
    int GentsBooked, int LadiesBooked, int GentsLeft, int LadiesLeft,
    /// <summary>Within the online cutoff (trip day and the 2 days before): customers see "Seats filled".</summary>
    bool IsBookingClosed);

public record TripInclusionsDto(
    bool Breakfast, bool Lunch, bool Dinner, bool Stay, bool Coordinator,
    bool AcVehicle, bool PushbackVehicle, bool Camping, bool Bonfire, bool MusicalNight, bool SwimmingPool);
