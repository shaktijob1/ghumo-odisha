using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Collections;

/// <summary>A trip in the Collections dropdown, with its departures that have confirmed bookings.</summary>
public record CollectionTripDto(int TripId, string Title, int Bookings, decimal Remaining, IReadOnlyList<CollectionDepartureDto> Departures);

public record CollectionDepartureDto(int TripDateSlotId, DateOnly StartDate, DateOnly EndDate, int Bookings, decimal Remaining);

public record CollectionTotalsDto(int Bookings, int Seats, decimal TotalAmount, decimal Paid, decimal Remaining);

public record CollectionBookingDto(
    int BookingId,
    string BookingReference,
    string CustomerName,
    string? CustomerPhone,
    DateOnly StartDate,
    DateOnly EndDate,
    int NumberOfSeats,
    decimal TotalAmount,
    decimal Paid,
    decimal Remaining,
    BookingStatus BookingStatus,
    PaymentStatus PaymentStatus);

public record CollectionSheetDto(int TripId, string TripTitle, int? TripDateSlotId, CollectionTotalsDto Totals, IReadOnlyList<CollectionBookingDto> Items);

public record PaymentQrDto(string ImageUrl, string? Caption, DateTime UpdatedAt);
