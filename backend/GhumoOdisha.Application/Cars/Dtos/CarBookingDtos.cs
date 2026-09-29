using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars.Dtos;

// ---------- Public search / details ----------

/// <summary>The customer's rental window: India date + time, and hours. Times are India time.</summary>
public record CarWindowQuery(DateOnly? Date, TimeOnly? Time, int? DurationHours);

public record CarSearchQuery(string? City, DateOnly? Date, TimeOnly? Time, int? DurationHours, int? Seats);

public record CarSearchResultDto(
    int CarId,
    string DisplayName,
    string Category,
    string Brand,
    string ModelName,
    FuelType FuelType,
    int SeatCapacity,
    bool HasAc,
    string BaseCity,
    string? CoverPhotoUrl,
    decimal PricePerKm,
    /// <summary>Lowest and highest base fare across the distance ranges (base fare falls as km grow).</summary>
    decimal BaseFareFrom,
    decimal BaseFareTo,
    decimal NightHaltPrice,
    /// <summary>False when the car is already booked for the searched window.</summary>
    bool IsAvailable);

public record CarSearchResultsDto(
    IReadOnlyList<CarSearchResultDto> Cars,
    /// <summary>Pickup cities that have listed cars — for the location picker.</summary>
    IReadOnlyList<string> Locations);

public record PublicDriverDto(string FirstName, string? ProfilePhotoUrl, int? ExperienceYears);

public record CarPublicDetailDto(
    CarSearchResultDto Summary,
    string? Description,
    IReadOnlyList<CarPhotoDto> Photos,
    IReadOnlyList<CarPricingTierDto> BaseFareTiers,
    PublicDriverDto Driver);

public record CarQuoteRequest(DateOnly PickupDate, TimeOnly PickupTime, int DurationHours, int EstimatedKm);

/// <summary>Server-calculated estimate for a window + distance — the booking page shows exactly this.</summary>
public record CarFareQuoteDto(
    int CarId,
    DateTime PickupAt,
    DateTime EndsAt,
    int DurationHours,
    int EstimatedKm,
    decimal PricePerKm,
    decimal BaseFare,
    decimal KmCharge,
    int Nights,
    decimal NightHaltPrice,
    decimal NightHaltCharge,
    decimal EstimatedTotal,
    decimal BookingAmount,
    decimal RemainingAmount,
    bool IsAvailable,
    string? UnavailableReason);

// ---------- Customer booking ----------

public record CreateCarBookingRequest(
    int CarId,
    DateOnly PickupDate,
    TimeOnly PickupTime,
    int DurationHours,
    int EstimatedKm,
    string? PickupAddress,
    string? CustomerNotes,
    Guid? ClientRequestId);

public record CancelCarBookingRequest(string? Reason);

public record FareBreakdownDto(
    int Km,
    decimal BaseFare,
    decimal KmCharge,
    int Nights,
    decimal NightHaltCharge,
    decimal AdditionalCharges,
    string? AdditionalChargesNote,
    decimal Total);

public record CarTripDto(
    DateTime StartedAt,
    int StartOdometerKm,
    DateTime? EndedAt,
    int? EndOdometerKm,
    int? ActualKm,
    int? NightHalts,
    DateTime? CompletedAt);

public record CarRefundDto(decimal Amount, CarPaymentStatus Status, PaymentMethod? Method, string? Reference, DateTime? IssuedAt, DateTime? SettledAt);

public record CarTimelineEventDto(string Title, string? Note, string ActorRole, DateTime CreatedAt);

/// <summary>Driver contact is only shown once the booking is confirmed (and not cancelled).</summary>
public record AssignedDriverDto(int DriverId, string Name, string? PhoneNumber, string? ProfilePhotoUrl);

public record CarBookingDto(
    int CarBookingId,
    string Reference,
    CarBookingStatus Status,
    CarPaymentStatus PaymentStatus,
    int CarId,
    string CarDisplayName,
    string Category,
    FuelType FuelType,
    int SeatCapacity,
    bool HasAc,
    string? CarPhotoUrl,
    /// <summary>Only after confirmation.</summary>
    string? RegistrationNumber,
    AssignedDriverDto? Driver,
    string PickupCity,
    string? PickupAddress,
    DateTime PickupAt,
    int DurationHours,
    DateTime EndsAt,
    decimal PricePerKm,
    decimal NightHaltPrice,
    FareBreakdownDto Estimate,
    FareBreakdownDto? Final,
    decimal BookingAmount,
    /// <summary>What's left to pay the driver: final total − booking amount once known, else the estimate's.</summary>
    decimal RemainingAmount,
    DateTime? BalanceCollectedAt,
    CarTripDto? Trip,
    CarRefundDto? Refund,
    DateTime? HoldExpiresAt,
    string? CustomerNotes,
    string? CancelledBy,
    string? CancellationReason,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    bool CanPay,
    bool CanCancel,
    IReadOnlyList<CarTimelineEventDto> Timeline);

// ---------- Admin ----------

public record AdminCarBookingCustomerDto(int CustomerId, string Name, string? PhoneNumber, string? Email);

public record AdminCarBookingDetailDto(
    CarBookingDto Booking,
    AdminCarBookingCustomerDto Customer,
    string? AdminNotes,
    string? RazorpayOrderId,
    string? RazorpayPaymentId,
    string? RazorpayRefundId,
    IReadOnlyList<CarAuditEventDto> History);

public record AdminCancelCarBookingRequest(string? Reason, bool WaiveRefund);

public record IssueCarRefundRequest(decimal Amount);

public record RecordCarManualRefundRequest(decimal Amount, PaymentMethod Method, string? Reference);

public record UpdateCarBookingNotesRequest(string? AdminNotes);
