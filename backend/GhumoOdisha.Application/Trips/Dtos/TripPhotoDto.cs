namespace GhumoOdisha.Application.Trips.Dtos;

public record TripPhotoDto(int TripPhotoId, string ImageUrl, int DisplayOrder);

public record RoomPhotoDto(int RoomPhotoId, string ImageUrl, int DisplayOrder);

public record VehiclePhotoDto(int VehiclePhotoId, string ImageUrl, int DisplayOrder);

public record PickupPointDto(int PickupPointId, string Location, string Time, int DisplayOrder);

public record TripHighlightDto(int TripHighlightId, string PlaceName, string Description, string PhotoUrl, int DisplayOrder);

public record ItineraryPointDto(int ItineraryPointId, string Time, string Description, int DisplayOrder);

public record ItineraryDayDto(int ItineraryDayId, int DayNumber, string Title, string Description, int DisplayOrder, IReadOnlyList<ItineraryPointDto> Points);

public record DateSlotDto(int TripDateSlotId, DateOnly StartDate, DateOnly EndDate, int TotalSeats, int AvailableSeats, bool IsSoldOut);

public record TripInclusionsDto(bool Breakfast, bool Lunch, bool Dinner, bool Stay, bool Coordinator);
