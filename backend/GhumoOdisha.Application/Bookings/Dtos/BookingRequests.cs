using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings.Dtos;

public record CreateBookingRequest(
    int TripId,
    int TripDateSlotId,
    int NumberOfSeats,
    string? CustomerNotes,
    Guid? ClientRequestId = null,
    int? PickupPointId = null,
    bool AgreedToTerms = false);

/// <summary><see cref="Method"/>/<see cref="PaymentReference"/> describe how the advance was received
/// (the admin's offline confirm); a Razorpay payment always records as <see cref="PaymentMethod.Razorpay"/>.</summary>
public record ConfirmBookingRequest(
    decimal AdvanceAmount,
    decimal DiscountAmount = 0,
    string? RazorpayPaymentId = null,
    PaymentMethod? Method = null,
    string? PaymentReference = null);

/// <summary>A later instalment collected offline (cash/UPI/bank) against a confirmed booking.</summary>
public record AddBookingPaymentRequest(
    decimal Amount,
    PaymentMethod Method,
    string? Reference,
    string? Notes,
    DateTime? PaidAt = null);

public record RejectBookingRequest(string? AdminNotes);

/// <summary><see cref="WaiveRefund"/> cancels without refunding anything paid — admin only, and
/// <see cref="Reason"/> is then required.</summary>
public record CancelBookingRequest(string? AdminNotes, bool WaiveRefund = false, string? Reason = null);

/// <summary>Admin change to the seat count of a booking; <see cref="Reason"/> is shown on the timeline.</summary>
public record ChangeSeatsRequest(int NumberOfSeats, string Reason);

public record UpdateGenderCountsRequest(int? MaleCount, int? FemaleCount);

public record TravellerInput(
    int SeatNumber,
    string FullName,
    Gender? Gender,
    int? Age,
    string? AadhaarLast4,
    string? PhoneNumber);

public record UpdateTravellersRequest(IReadOnlyList<TravellerInput> Travellers);

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
