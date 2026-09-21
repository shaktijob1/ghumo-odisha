using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Customers;
using GhumoOdisha.Application.Customers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/customer/profile")]
[Authorize(Roles = "Customer")]
public class CustomerProfileController(ICustomerService customerService) : ControllerBase
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
}
