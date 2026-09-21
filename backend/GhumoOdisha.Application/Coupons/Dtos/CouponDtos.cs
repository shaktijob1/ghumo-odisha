namespace GhumoOdisha.Application.Coupons.Dtos;

public record AdminCreateCouponRequest(string Code, decimal DiscountAmount, DateOnly? ValidFrom, DateOnly? ValidUntil);

public record AdminUpdateCouponRequest(decimal DiscountAmount, DateOnly? ValidFrom, DateOnly? ValidUntil, bool IsActive);

public record AdminCouponDto(
    int CouponCodeId,
    string Code,
    decimal DiscountAmount,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsActive,
    int RedemptionCount,
    DateTime CreatedAt);

public record ValidateCouponRequest(string Code);

public record ValidateCouponResult(string Code, decimal DiscountAmount);

public record CouponValidation(int CouponCodeId, decimal DiscountAmount);
