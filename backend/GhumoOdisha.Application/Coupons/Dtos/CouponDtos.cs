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
    DateTime CreatedAt);

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
