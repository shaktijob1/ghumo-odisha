using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminTripBookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet("api/admin/trips/{tripId:int}/bookings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminBookingListItemDto>>>> GetTripBookings(int tripId, CancellationToken cancellationToken)
    {
        var result = await bookingService.GetBookingsForTripAsync(tripId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminBookingListItemDto>>.Ok(result));
    }

    [HttpGet("api/admin/date-slots/{dateSlotId:int}/bookings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminBookingListItemDto>>>> GetDateSlotBookings(int dateSlotId, CancellationToken cancellationToken)
    {
        var result = await bookingService.GetBookingsForDateSlotAsync(dateSlotId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminBookingListItemDto>>.Ok(result));
    }
}
