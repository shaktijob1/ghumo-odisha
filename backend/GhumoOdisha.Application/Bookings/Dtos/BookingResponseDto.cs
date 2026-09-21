using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings.Dtos;

public record BookingResponseDto(
    int BookingId,
    int TripId,
    string TripTitle,
    string? TripCoverImageUrl,
    int TripDateSlotId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? PickupPointLocation,
    string? PickupPointTime,
    int NumberOfSeats,
    decimal AmountPerPerson,
    decimal TotalAmount,
    decimal AdvanceAmount,
    decimal RemainingAmount,
    BookingStatus BookingStatus,
    PaymentStatus PaymentStatus,
    BookingSource BookingSource,
    string? CustomerNotes,
    string? AdminNotes,
    DateTime RequestedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt);

public record CreateBookingResult(BookingResponseDto Booking, string WhatsAppMessage);
