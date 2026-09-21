using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminItineraryController(ITripService tripService) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/itinerary-days")]
    public async Task<ActionResult<ApiResponse<object>>> AddDay(int tripId, AddItineraryDayRequest request, CancellationToken cancellationToken)
    {
        var dayId = await tripService.AddItineraryDayAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { itineraryDayId = dayId }, "Itinerary day added."));
    }

    [HttpPut("api/admin/itinerary-days/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDay(int id, UpdateItineraryDayRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateItineraryDayAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Itinerary day updated."));
    }

    [HttpDelete("api/admin/itinerary-days/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDay(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteItineraryDayAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Itinerary day deleted."));
    }

    [HttpPost("api/admin/itinerary-days/{dayId:int}/points")]
    public async Task<ActionResult<ApiResponse<object>>> AddPoint(int dayId, AddItineraryPointRequest request, CancellationToken cancellationToken)
    {
        var pointId = await tripService.AddItineraryPointAsync(dayId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { itineraryPointId = pointId }, "Itinerary point added."));
    }

    [HttpPut("api/admin/itinerary-points/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePoint(int id, UpdateItineraryPointRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateItineraryPointAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Itinerary point updated."));
    }

    [HttpDelete("api/admin/itinerary-points/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePoint(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteItineraryPointAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Itinerary point deleted."));
    }
}
