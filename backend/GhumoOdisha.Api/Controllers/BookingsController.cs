using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
public class BookingsController(IBookingService bookingService, IInvoiceService invoiceService, IBookingEmailService bookingEmails) : ControllerBase
{
    [HttpPost("api/bookings/request")]
    public async Task<ActionResult<ApiResponse<CreateBookingResult>>> RequestBooking(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await bookingService.RequestBookingAsync(customerId, request, cancellationToken);
        return Ok(ApiResponse<CreateBookingResult>.Ok(result, "Booking request submitted."));
    }

    [HttpGet("api/customer/bookings")]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingResponseDto>>>> GetMyBookings(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var customerId = User.GetCustomerId();
        var result = await bookingService.GetCustomerBookingsAsync(customerId, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedResult<BookingResponseDto>>.Ok(result));
    }

    [HttpGet("api/customer/bookings/{id:int}")]
    public async Task<ActionResult<ApiResponse<BookingResponseDto>>> GetMyBookingDetail(int id, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await bookingService.GetCustomerBookingDetailAsync(customerId, id, cancellationToken);
        return Ok(ApiResponse<BookingResponseDto>.Ok(result));
    }

    [HttpGet("api/customer/bookings/{id:int}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int id, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var pdf = await invoiceService.GenerateInvoicePdfAsync(customerId, id, cancellationToken);
        return File(pdf, "application/pdf", "GhumoOdisha-Invoice.pdf");
    }

    [HttpPost("api/customer/bookings/{id:int}/invoice/email")]
    [EnableRateLimiting("InvoiceEmail")]
    public async Task<ActionResult<ApiResponse<object?>>> EmailInvoice(int id, EmailInvoiceRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        await bookingEmails.SendInvoiceAsync(customerId, id, request.Email.Trim(), cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, "Invoice sent to your email."));
    }

    [HttpPost("api/customer/bookings/{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingResponseDto>>> CancelMyBooking(int id, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await bookingService.CancelOwnBookingAsync(customerId, id, cancellationToken);
        return Ok(ApiResponse<BookingResponseDto>.Ok(result, "Booking cancelled."));
    }
}
