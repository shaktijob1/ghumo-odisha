using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminPickupPointsController(ITripService tripService) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/pickup-points")]
    public async Task<ActionResult<ApiResponse<object>>> AddPickupPoint(int tripId, AddPickupPointRequest request, CancellationToken cancellationToken)
    {
        var pickupPointId = await tripService.AddPickupPointAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { pickupPointId }, "Pickup point added."));
    }

    [HttpPut("api/admin/pickup-points/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePickupPoint(int id, UpdatePickupPointRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdatePickupPointAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Pickup point updated."));
    }

    [HttpDelete("api/admin/pickup-points/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePickupPoint(int id, CancellationToken cancellationToken)
    {
        await tripService.DeletePickupPointAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Pickup point deleted."));
    }
}
