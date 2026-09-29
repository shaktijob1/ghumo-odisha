using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Driver sign-in: WhatsApp OTP or Google (same methods as customers, separate Driver account).</summary>
[ApiController]
[Route("api/auth/driver")]
[EnableRateLimiting("AuthIp")]
public class DriverAuthController(IDriverAuthService driverAuthService) : ControllerBase
{
    [HttpPost("request-otp")]
    public async Task<ActionResult<ApiResponse<RequestOtpResponse>>> RequestOtp(RequestOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await driverAuthService.RequestOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<RequestOtpResponse>.Ok(result, "OTP sent."));
    }

    [HttpPost("verify-otp")]
    public async Task<ActionResult<ApiResponse<DriverAuthResponse>>> VerifyOtp(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await driverAuthService.VerifyOtpAsync(request, cancellationToken);
        return Ok(ApiResponse<DriverAuthResponse>.Ok(result, "Signed in."));
    }

    [HttpPost("google")]
    public async Task<ActionResult<ApiResponse<DriverAuthResponse>>> Google(GoogleSignInRequest request, CancellationToken cancellationToken)
    {
        var result = await driverAuthService.GoogleSignInAsync(request, cancellationToken);
        return Ok(ApiResponse<DriverAuthResponse>.Ok(result, "Signed in with Google."));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<DriverAuthResponse>>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await driverAuthService.RefreshAsync(request, cancellationToken);
        return Ok(ApiResponse<DriverAuthResponse>.Ok(result));
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ApiResponse<object>>> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await driverAuthService.LogoutAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Signed out."));
    }
}
