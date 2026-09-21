using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/trips")]
public class TripsController(ITripService tripService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TripSummaryDto>>>> GetActiveTrips(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await tripService.GetActiveTripsAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<TripSummaryDto>>.Ok(result));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TripDetailDto>>> GetTripDetail(int id, CancellationToken cancellationToken)
    {
        var result = await tripService.GetTripDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<TripDetailDto>.Ok(result));
    }

    [HttpGet("{id:int}/date-slots")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DateSlotDto>>>> GetDateSlots(int id, CancellationToken cancellationToken)
    {
        var result = await tripService.GetActiveDateSlotsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DateSlotDto>>.Ok(result));
    }
}
