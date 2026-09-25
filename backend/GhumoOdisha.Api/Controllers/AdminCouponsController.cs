using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Coupons.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/coupons")]
[Authorize(Roles = "Admin")]
public class AdminCouponsController(ICouponService couponService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminCouponDto>>>> GetCoupons(CancellationToken cancellationToken)
    {
        var result = await couponService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminCouponDto>>.Ok(result));
    }

    [HttpGet("{id:int}/bookings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminCouponBookingDto>>>> GetCouponBookings(int id, CancellationToken cancellationToken)
    {
        var result = await couponService.GetBookingsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminCouponBookingDto>>.Ok(result));
    }

    [HttpGet("{id:int}/payouts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CouponPayoutDto>>>> GetPayouts(int id, CancellationToken cancellationToken)
    {
        var result = await couponService.GetPayoutsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CouponPayoutDto>>.Ok(result));
    }

    [HttpPost("{id:int}/payouts")]
    public async Task<ActionResult<ApiResponse<object>>> AddPayout(int id, AddCouponPayoutRequest request, CancellationToken cancellationToken)
    {
        await couponService.AddPayoutAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Payout recorded."));
    }

    [HttpDelete("{id:int}/payouts/{payoutId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> RemovePayout(int id, int payoutId, CancellationToken cancellationToken)
    {
        await couponService.RemovePayoutAsync(id, payoutId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Payout removed."));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateCoupon(AdminCreateCouponRequest request, CancellationToken cancellationToken)
    {
        var couponCodeId = await couponService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { couponCodeId }, "Coupon created."));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCoupon(int id, AdminUpdateCouponRequest request, CancellationToken cancellationToken)
    {
        await couponService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Coupon updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCoupon(int id, CancellationToken cancellationToken)
    {
        await couponService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Coupon removed."));
    }
}
