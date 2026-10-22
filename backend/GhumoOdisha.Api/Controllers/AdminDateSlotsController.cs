using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminDateSlotsController(ITripService tripService) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/date-slots")]
    public async Task<ActionResult<ApiResponse<object>>> AddDateSlot(int tripId, AddDateSlotRequest request, CancellationToken cancellationToken)
    {
        var slotId = await tripService.AddDateSlotAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tripDateSlotId = slotId }, "Date slot added."));
    }

    [HttpPut("api/admin/date-slots/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDateSlot(int id, UpdateDateSlotRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateDateSlotAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Date slot updated."));
    }

    /// <summary>One price for every departure of the trip that starts in a month (empty price = back to the trip's price).</summary>
    [HttpPut("api/admin/trips/{tripId:int}/date-slots/month-price")]
    public async Task<ActionResult<ApiResponse<object>>> SetMonthPrice(int tripId, SetMonthPriceRequest request, CancellationToken cancellationToken)
    {
        var count = await tripService.SetMonthPriceAsync(tripId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { updated = count }, $"Price updated on {count} date(s)."));
    }

    [HttpDelete("api/admin/date-slots/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDateSlot(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteDateSlotAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Date slot deleted."));
    }
}
