using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Coupons.Dtos;

public record AdminCreateCouponRequest(
    string Code,
    string HolderName,
    decimal DiscountAmount,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsFirstTimeCustomerOnly = false,
    decimal CommissionPerSeat = CouponDefaults.CommissionPerSeat);

public record AdminUpdateCouponRequest(
    string HolderName,
    decimal DiscountAmount,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsActive,
    bool IsFirstTimeCustomerOnly = false,
    decimal CommissionPerSeat = CouponDefaults.CommissionPerSeat);

public record AdminCouponDto(
    int CouponCodeId,
    string Code,
    string HolderName,
    decimal DiscountAmount,
    decimal CommissionPerSeat,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsActive,
    bool IsFirstTimeCustomerOnly,
    int RedemptionCount,
    int NewCustomerCount,
    int ExistingCustomerCount,
    int TotalSeats,
    decimal CommissionEarned,
    DateTime CreatedAt,
    int ActiveBookingCount,
    int CancelledBookingCount,
    // Commission recorded at redemption but lost because the booking was cancelled/rejected.
    decimal CommissionReversed,
    decimal CommissionPaid,
    // Earned − paid. Negative when a booking was cancelled after the holder was paid — it's
    // netted against their future commission.
    decimal BalanceDue);

public record CouponPayoutDto(
    int CouponPayoutId,
    decimal Amount,
    PaymentMethod Method,
    string? Reference,
    string? Notes,
    DateTime PaidAt);

public record AddCouponPayoutRequest(
    decimal Amount,
    PaymentMethod Method,
    string? Reference,
    string? Notes,
    DateTime? PaidAt = null);

/// <summary>One booking made with a coupon. <see cref="CommissionEarned"/> is zero when the booking
/// was later cancelled or rejected; <see cref="CommissionAmount"/> is what it earned at redemption.</summary>
public record AdminCouponBookingDto(
    int BookingId,
    int CustomerId,
    string CustomerName,
    string CustomerPhone,
    bool IsNewCustomer,
    string TripTitle,
    DateOnly StartDate,
    int NumberOfSeats,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal CommissionAmount,
    decimal CommissionEarned,
    BookingStatus BookingStatus,
    DateTime RedeemedAt);

public record ValidateCouponRequest(string Code);

public record ValidateCouponResult(string Code, decimal DiscountAmount);

public record CouponValidation(int CouponCodeId, decimal DiscountAmount);

public static class CouponDefaults
{
    public const decimal CommissionPerSeat = 200m;
}

/// <summary>
/// What a coupon holder (influencer / agent) sees on the public partner page when they enter
/// their code: their own totals and payouts only — never who booked (no names, phones or booking ids).
/// </summary>
public record PartnerCouponSummaryDto(
    string Code,
    string HolderName,
    decimal DiscountAmount,
    decimal CommissionPerSeat,
    bool IsActive,
    DateOnly? ValidUntil,
    int ActiveBookings,
    int CancelledBookings,
    int SeatsTravelling,
    decimal CommissionEarned,
    decimal CommissionReversed,
    decimal CommissionPaid,
    decimal BalanceDue,
    IReadOnlyList<PartnerPayoutDto> Payouts,
    IReadOnlyList<PartnerActivityDto> Activity);

public record PartnerPayoutDto(DateTime PaidAt, decimal Amount, PaymentMethod Method);

/// <summary>One booking made with the code, anonymised: which trip, how many seats, what it earned.</summary>
public record PartnerActivityDto(DateTime BookedOn, string TripTitle, DateOnly TravelDate, int Seats, decimal Commission, bool Counts);
