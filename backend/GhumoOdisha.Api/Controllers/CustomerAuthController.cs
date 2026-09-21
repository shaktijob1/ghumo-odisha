using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/auth/customer")]
[EnableRateLimiting("AuthIp")]
public class CustomerAuthController(ICustomerAuthService customerAuthService) : ControllerBase
{
    [HttpPost("request-otp")]
    public async Task<ActionResult<ApiResponse<RequestOtpResponse>>> RequestOtp(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await customerAuthService.RequestOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<RequestOtpResponse>.Ok(result, "OTP sent."));
    }

    [HttpPost("verify-otp")]
    public async Task<ActionResult<ApiResponse<CustomerAuthResponse>>> VerifyOtp(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await customerAuthService.VerifyOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(result, "WhatsApp number verified successfully."));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<CustomerAuthResponse>>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await customerAuthService.RefreshAsync(request, cancellationToken);
        return Ok(ApiResponse<CustomerAuthResponse>.Ok(result));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await customerAuthService.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Signed out."));
    }
}
