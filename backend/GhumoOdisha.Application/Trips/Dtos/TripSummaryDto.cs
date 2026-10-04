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
    IReadOnlyList<string> DestinationNames,
    IReadOnlyList<UpcomingSlotDto> UpcomingSlots,
    // Bookable departures not already among UpcomingSlots (e.g. after a searched month that is
    // fully booked) — the card offers these when every date it shows is full.
    IReadOnlyList<UpcomingSlotDto> NextOpenSlots);

/// <summary>Display-only preview of a departure for the trip card's rolling dates strip.</summary>
public record UpcomingSlotDto(DateOnly StartDate, int AvailableSeats);
