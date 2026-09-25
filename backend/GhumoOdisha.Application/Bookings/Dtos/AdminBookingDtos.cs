using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings.Dtos;

public record AdminBookingListItemDto(
    int BookingId,
    int CustomerId,
    string CustomerName,
    string CustomerPhone,
    int TripId,
    string TripTitle,
    int TripDateSlotId,
    DateOnly StartDate,
    DateOnly EndDate,
    int NumberOfSeats,
    decimal TotalAmount,
    decimal AdvanceAmount,
    BookingStatus BookingStatus,
    PaymentStatus PaymentStatus,
    BookingSource BookingSource,
    DateTime RequestedAt);

public record AdminBookingDetailDto(
    int BookingId,
    int CustomerId,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    int CustomerBookingCount,
    int TripId,
    string TripTitle,
    int TripDateSlotId,
    DateOnly StartDate,
    DateOnly EndDate,
    int SlotAvailableSeats,
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
    decimal DiscountAmount,
    string? CouponCode,
    IReadOnlyList<BookingPaymentDto> Payments,
    int RoomsAllotted,
    int? MaleCount,
    int? FemaleCount,
    string? CancellationReason,
    bool RefundWaived,
    IReadOnlyList<AdminTravellerDto> Travellers,
    IReadOnlyList<BookingEventDto> Timeline);

public record AdminTravellerDto(
    int BookingTravellerId,
    int SeatNumber,
    string FullName,
    Gender? Gender,
    int? Age,
    string? AadhaarLast4,
    string? PhoneNumber,
    int? LinkedCustomerId,
    string? LinkedCustomerName);

public record BookingEventDto(
    BookingEventType EventType,
    string Title,
    string? Description,
    string Actor,
    DateTime CreatedAt);

public record BookingPaymentDto(
    int BookingPaymentId,
    decimal Amount,
    PaymentMethod Method,
    string? Reference,
    string? Notes,
    string RecordedBy,
    DateTime PaidAt);

public record AdminBookingFilter(
    BookingStatus? BookingStatus = null,
    PaymentStatus? PaymentStatus = null,
    int? TripId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Search = null);
