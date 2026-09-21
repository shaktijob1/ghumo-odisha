using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings.Dtos;

public record CreateBookingRequest(int TripId, int TripDateSlotId, int NumberOfSeats, string? CustomerNotes, Guid? ClientRequestId = null, int? PickupPointId = null);

public record ConfirmBookingRequest(decimal AdvanceAmount, decimal DiscountAmount = 0, string? RazorpayPaymentId = null);

public record RejectBookingRequest(string? AdminNotes);

public record CancelBookingRequest(string? AdminNotes);

public record CreateManualBookingRequest(
    int? CustomerId,
    string? NewCustomerName,
    string? NewCustomerPhoneNumber,
    string? NewCustomerEmail,
    int TripId,
    int TripDateSlotId,
    int NumberOfSeats,
    decimal AdvanceAmount,
    BookingSource BookingSource,
    BookingStatus InitialStatus,
    string? AdminNotes);
