using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Customers;
using GhumoOdisha.Application.Customers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/customer/profile")]
[Authorize(Roles = "Customer")]
public class CustomerProfileController(ICustomerService customerService, ICustomerAuthService customerAuthService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<CustomerProfileDto>>> GetProfile(CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await customerService.GetProfileAsync(customerId, cancellationToken);
        return Ok(ApiResponse<CustomerProfileDto>.Ok(result));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        await customerService.UpdateProfileAsync(customerId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Profile updated."));
    }

    [HttpPost("phone/request-otp")]
    [EnableRateLimiting("AuthIp")]
    public async Task<ActionResult<ApiResponse<RequestOtpResponse>>> RequestAddPhoneOtp(AddPhoneRequest request, CancellationToken cancellationToken)
    {
        var result = await customerAuthService.RequestAddPhoneOtpAsync(User.GetCustomerId(), request, cancellationToken);
        return Ok(ApiResponse<RequestOtpResponse>.Ok(result, "OTP sent."));
    }

    [HttpPost("phone/verify")]
    [EnableRateLimiting("AuthIp")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyAddPhone(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        await customerAuthService.VerifyAddPhoneOtpAsync(User.GetCustomerId(), request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "WhatsApp number added."));
    }

    [HttpPost("google")]
    [EnableRateLimiting("AuthIp")]
    public async Task<ActionResult<ApiResponse<object>>> LinkGoogle(GoogleSignInRequest request, CancellationToken cancellationToken)
    {
        await customerAuthService.LinkGoogleAsync(User.GetCustomerId(), request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Google account linked."));
    }
}
