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
    DateTime? CancelledAt,
    int SlotAvailableSeats,
    // False when the caller is a traveller the organizer added to someone else's booking —
    // view-only: paying, cancelling and the invoice stay with the person who booked.
    bool IsOwner,
    int RoomsAllotted,
    IReadOnlyList<BookingEventDto> Timeline,
    // Only for the booking's owner — a linked traveller doesn't see the booker's payment details.
    IReadOnlyList<BookingPaymentDto> Payments);

public record CreateBookingResult(BookingResponseDto Booking, string WhatsAppMessage);
