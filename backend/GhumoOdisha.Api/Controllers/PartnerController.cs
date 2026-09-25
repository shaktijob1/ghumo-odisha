using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Coupons.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

/// <summary>
/// Public endpoint behind the influencer/partner earnings page — no login, just the coupon code.
/// Rate-limited per IP so codes can't be guessed by brute force.
/// </summary>
[ApiController]
[Route("api/partner")]
[AllowAnonymous]
[EnableRateLimiting("PartnerLookup")]
public class PartnerController(ICouponService couponService) : ControllerBase
{
    [HttpGet("coupons/{code}")]
    public async Task<ActionResult<ApiResponse<PartnerCouponSummaryDto>>> GetSummary(string code, CancellationToken cancellationToken)
    {
        var result = await couponService.GetPartnerSummaryAsync(code, cancellationToken);
        return Ok(ApiResponse<PartnerCouponSummaryDto>.Ok(result));
    }
}
