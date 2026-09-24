using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

public record AdminTripListItemDto(
    int TripId,
    string Title,
    decimal AmountPerPerson,
    TripStatus Status,
    int DateSlotCount,
    DateOnly? NextSlotStartDate,
    int? NextSlotTotalSeats,
    int? NextSlotAvailableSeats,
    int ConfirmedBookingCount);

public record AdminTripDetailDto(
    int TripId,
    string Title,
    string Description,
    decimal AmountPerPerson,
    TripInclusionsDto Inclusions,
    TripStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TripPhotoDto> Photos,
    IReadOnlyList<TripHighlightDto> Highlights,
    IReadOnlyList<ItineraryDayDto> ItineraryDays,
    IReadOnlyList<RoomPhotoDto> RoomPhotos,
    IReadOnlyList<VehiclePhotoDto> VehiclePhotos,
    IReadOnlyList<PickupPointDto> PickupPoints,
    IReadOnlyList<DateSlotDto> DateSlots,
    IReadOnlyList<int> DestinationIds,
    IReadOnlyList<string> DestinationNames,
    string? ItineraryPdfUrl);
