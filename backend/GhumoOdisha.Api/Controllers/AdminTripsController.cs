using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/trips")]
[Authorize(Roles = "Admin")]
public class AdminTripsController(ITripService tripService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminTripListItemDto>>>> GetTrips(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] TripStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await tripService.GetAdminTripsAsync(page, pageSize, search, status, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminTripListItemDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateTrip(CreateTripRequest request, CancellationToken cancellationToken)
    {
        var tripId = await tripService.CreateTripAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tripId }, "Trip created."));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminTripDetailDto>>> GetTripDetail(int id, CancellationToken cancellationToken)
    {
        var result = await tripService.GetAdminTripDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminTripDetailDto>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTrip(int id, UpdateTripRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateTripAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Trip updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTrip(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteTripAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Trip deactivated."));
    }
}
