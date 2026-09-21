using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Coupons.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
public class CustomerCouponsController(ICouponService couponService) : ControllerBase
{
    [HttpPost("api/customer/coupons/validate")]
    public async Task<ActionResult<ApiResponse<ValidateCouponResult>>> Validate(ValidateCouponRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await couponService.ValidateAsync(request.Code, customerId, cancellationToken);
        return Ok(ApiResponse<ValidateCouponResult>.Ok(new ValidateCouponResult(request.Code.Trim().ToUpperInvariant(), result.DiscountAmount)));
    }
}
