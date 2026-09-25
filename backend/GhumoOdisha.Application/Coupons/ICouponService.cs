using GhumoOdisha.Application.Coupons.Dtos;

namespace GhumoOdisha.Application.Coupons;

public interface ICouponService
{
    // ---------- Admin ----------
    Task<int> CreateAsync(AdminCreateCouponRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(int couponCodeId, AdminUpdateCouponRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int couponCodeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminCouponDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Every booking made with this coupon, with the customer, new/existing flag and commission.</summary>
    Task<IReadOnlyList<AdminCouponBookingDto>> GetBookingsAsync(int couponCodeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CouponPayoutDto>> GetPayoutsAsync(int couponCodeId, CancellationToken cancellationToken = default);
    /// <summary>Records commission paid to the holder. Rejects an amount above the current balance due.</summary>
    Task AddPayoutAsync(int couponCodeId, AddCouponPayoutRequest request, CancellationToken cancellationToken = default);
    /// <summary>Removes a wrongly recorded payout.</summary>
    Task RemovePayoutAsync(int couponCodeId, int couponPayoutId, CancellationToken cancellationToken = default);

    // ---------- Public partner page ----------
    /// <summary>A coupon holder's own totals and payouts, looked up by code. Contains nothing about who booked.</summary>
    Task<PartnerCouponSummaryDto> GetPartnerSummaryAsync(string code, CancellationToken cancellationToken = default);

    // ---------- Customer ----------

    /// <summary>Checks the code exists, is active, in its validity window, and not already used by
    /// this customer — without redeeming it. Throws NotFoundException / ConflictException on failure.</summary>
    Task<CouponValidation> ValidateAsync(string code, int customerId, CancellationToken cancellationToken = default);

    /// <summary>Records the redemption. Throws ConflictException if this customer already redeemed
    /// this coupon (races the unique index as the final guard).</summary>
    Task RedeemAsync(int couponCodeId, int customerId, int bookingId, decimal discountAmount, CancellationToken cancellationToken = default);
}
