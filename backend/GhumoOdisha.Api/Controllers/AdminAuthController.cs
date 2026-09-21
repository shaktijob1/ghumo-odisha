using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
[EnableRateLimiting("AuthIp")]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AdminAuthResponse>>> Login(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await adminAuthService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AdminAuthResponse>.Ok(result, "Login successful."));
    }
}
