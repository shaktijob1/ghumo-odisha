namespace GhumoOdisha.Application.Trips.Dtos;

public record TripDetailDto(
    int TripId,
    string Title,
    string Description,
    decimal AmountPerPerson,
    TripInclusionsDto Inclusions,
    IReadOnlyList<TripPhotoDto> Photos,
    IReadOnlyList<TripHighlightDto> Highlights,
    IReadOnlyList<ItineraryDayDto> ItineraryDays,
    IReadOnlyList<RoomPhotoDto> RoomPhotos,
    IReadOnlyList<VehiclePhotoDto> VehiclePhotos,
    IReadOnlyList<PickupPointDto> PickupPoints,
    IReadOnlyList<DateSlotDto> DateSlots);
