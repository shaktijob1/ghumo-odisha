using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Customers;
using GhumoOdisha.Application.Customers.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/customers")]
[Authorize(Roles = "Admin")]
public class AdminCustomersController(ICustomerService customerService, IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminCustomerListItemDto>>>> GetCustomers(
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await customerService.GetAdminCustomersAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminCustomerListItemDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminCustomerDetailDto>>> GetCustomerDetail(int id, CancellationToken cancellationToken)
    {
        var result = await customerService.GetAdminCustomerDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminCustomerDetailDto>.Ok(result));
    }

    [HttpGet("{id:int}/bookings")]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingResponseDto>>>> GetCustomerBookings(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await bookingService.GetCustomerBookingsAsync(id, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<BookingResponseDto>>.Ok(result));
    }
}
