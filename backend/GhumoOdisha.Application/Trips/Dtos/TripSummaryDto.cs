namespace GhumoOdisha.Application.Trips.Dtos;

public record TripSummaryDto(
    int TripId,
    string Title,
    decimal AmountPerPerson,
    string? CoverImageUrl,
    string? DurationLabel,
    DateOnly? NextSlotStartDate,
    DateOnly? NextSlotEndDate,
    int? NextSlotAvailableSeats,
    int? NextSlotTotalSeats,
    TripInclusionsDto Inclusions,
    IReadOnlyList<string> HighlightPlaceNames,
    IReadOnlyList<TripPhotoDto> Photos,
    IReadOnlyList<string> DestinationNames);
