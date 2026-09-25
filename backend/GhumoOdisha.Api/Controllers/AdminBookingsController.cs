using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Roles = "Admin")]
public class AdminBookingsController(IBookingService bookingService, IBookingTravellerService travellerService, IInvoiceService invoiceService) : ControllerBase
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

    [HttpPost("{id:int}/payments")]
    public async Task<ActionResult<ApiResponse<object>>> AddPayment(int id, AddBookingPaymentRequest request, CancellationToken cancellationToken)
    {
        await bookingService.AddPaymentAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Payment recorded."));
    }

    [HttpDelete("{id:int}/payments/{paymentId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> RemovePayment(int id, int paymentId, CancellationToken cancellationToken)
    {
        await bookingService.RemovePaymentAsync(id, paymentId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Payment removed."));
    }

    [HttpPost("{id:int}/seats")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeSeats(int id, ChangeSeatsRequest request, CancellationToken cancellationToken)
    {
        await bookingService.ChangeSeatsAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Seats updated."));
    }

    [HttpPut("{id:int}/travellers")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTravellers(int id, UpdateTravellersRequest request, CancellationToken cancellationToken)
    {
        await travellerService.UpdateTravellersAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Traveller details saved."));
    }

    [HttpPut("{id:int}/gender-counts")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateGenderCounts(int id, UpdateGenderCountsRequest request, CancellationToken cancellationToken)
    {
        await travellerService.UpdateGenderCountsAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Male / female count saved."));
    }

    [HttpGet("{id:int}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int id, CancellationToken cancellationToken)
    {
        var pdf = await invoiceService.GenerateAdminInvoicePdfAsync(id, cancellationToken);
        return File(pdf, "application/pdf", $"GhumoOdisha-Invoice-GO-{id}.pdf");
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
