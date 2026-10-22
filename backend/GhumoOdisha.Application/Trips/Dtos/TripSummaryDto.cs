using GhumoOdisha.Domain.Enums;

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
    IReadOnlyList<UpcomingSlotDto> NextOpenSlots,
    /// <summary>Lowest per-person price bookable today across open departures (the trip price when none differ) — the "From ₹…" price.</summary>
    decimal FromPrice,
    /// <summary>Open, or marked Booked / Closed by the admin (the card shows a "Fully booked" / "Bookings closed" ribbon).</summary>
    TripBookingState BookingState,
    /// <summary>Category keys ("PoolParty", "Beach"…) for the home page filter.</summary>
    IReadOnlyList<string> Categories,
    /// <summary>The admin's own days / nights (null = counted from the dates) — the card's "3D/2N".</summary>
    int? DurationDays = null,
    int? DurationNights = null);

/// <summary>Display-only preview of a departure for the trip card's rolling dates strip.</summary>
public record UpcomingSlotDto(DateOnly StartDate, int AvailableSeats);

/// <summary>A home page filter chip: category key ("Beach") and how many bookable trips it has.</summary>
public record TripCategoryCountDto(string Category, int TripCount);
