using GhumoOdisha.Application.Seo;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

/// <param name="Destinations">Destination pages this trip covers (linked from the trip page).</param>
/// <param name="DepartureCity">The city the trip leaves from — always Bhubaneswar.</param>
/// <param name="DurationLabel">"4 Days / 3 Nights", from the next upcoming departure.</param>
/// <param name="PlacesCovered">Every place the trip visits, as entered by the admin.</param>
/// <param name="Faqs">"Good to know" questions built from this trip's data and the booking rules.</param>
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
    IReadOnlyList<DateSlotDto> DateSlots,
    string? ItineraryPdfUrl,
    IReadOnlyList<TripDestinationLink> Destinations,
    string? DepartureCity,
    string? DurationLabel,
    IReadOnlyList<string> PlacesCovered,
    IReadOnlyList<FaqItem> Faqs,
    /// <summary>Lowest per-person price bookable today across open departures — the "From ₹…" price.</summary>
    decimal FromPrice,
    TripBookingState BookingState,
    IReadOnlyList<string> Categories,
    int? DurationDays = null,
    int? DurationNights = null);
