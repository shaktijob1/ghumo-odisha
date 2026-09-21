using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Roles = "Admin")]
public class AdminBookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminBookingListItemDto>>>> GetBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] BookingStatus? bookingStatus = null,
        [FromQuery] PaymentStatus? paymentStatus = null,
        [FromQuery] int? tripId = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new AdminBookingFilter(bookingStatus, paymentStatus, tripId, fromDate, toDate, search);
        var result = await bookingService.GetAdminBookingsAsync(filter, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminBookingListItemDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateManualBooking(CreateManualBookingRequest request, CancellationToken cancellationToken)
    {
        var bookingId = await bookingService.CreateManualBookingAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { bookingId }, "Booking created."));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminBookingDetailDto>>> GetBookingDetail(int id, CancellationToken cancellationToken)
    {
        var result = await bookingService.GetAdminBookingDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminBookingDetailDto>.Ok(result));
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmBooking(int id, ConfirmBookingRequest request, CancellationToken cancellationToken)
    {
        await bookingService.ConfirmBookingAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Booking confirmed."));
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<object>>> RejectBooking(int id, RejectBookingRequest request, CancellationToken cancellationToken)
    {
        await bookingService.RejectBookingAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Booking rejected."));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> CancelBooking(int id, CancelBookingRequest request, CancellationToken cancellationToken)
    {
        await bookingService.CancelBookingAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Booking cancelled."));
    }
}
