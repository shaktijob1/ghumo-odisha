using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin view of car bookings: list, detail + full history, cancel, notes and the booking-amount refund desk.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/car-bookings")]
public class AdminCarBookingsController(ICarBookingService bookingService, ICarTripService tripService) : ControllerBase
{
    /// <summary>Correct a completed trip's readings / night halts / extras; the final fare is recalculated and the change audited.</summary>
    [HttpPost("{id:int}/correct-fare")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> CorrectFare(int id, AdminCorrectFareRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await tripService.AdminCorrectFareAsync(User.GetAdminId(), id, request, cancellationToken), "Final fare corrected."));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CarBookingSummaryDto>>>> List(
        [FromQuery] CarBookingStatus? status = null, [FromQuery] CarPaymentStatus? paymentStatus = null, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<CarBookingSummaryDto>>.Ok(await bookingService.ListAsync(status, paymentStatus, search, page, pageSize, cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await bookingService.GetForAdminAsync(id, cancellationToken)));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> Cancel(int id, AdminCancelCarBookingRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await bookingService.CancelByAdminAsync(User.GetAdminId(), id, request, cancellationToken), "Booking cancelled."));

    [HttpPut("{id:int}/notes")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> Notes(int id, UpdateCarBookingNotesRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await bookingService.UpdateNotesAsync(id, request.AdminNotes, cancellationToken), "Notes saved."));

    [HttpPost("{id:int}/refund/razorpay")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> RefundRazorpay(int id, IssueCarRefundRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await bookingService.IssueRazorpayRefundAsync(User.GetAdminId(), id, request.Amount, cancellationToken), "Refund issued via Razorpay."));

    [HttpPost("{id:int}/refund/manual")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> RefundManual(int id, RecordCarManualRefundRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await bookingService.RecordManualRefundAsync(User.GetAdminId(), id, request, cancellationToken), "Manual refund recorded."));

    [HttpPost("{id:int}/refund/settle")]
    public async Task<ActionResult<ApiResponse<AdminCarBookingDetailDto>>> SettleRefund(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarBookingDetailDto>.Ok(await bookingService.SettleRefundAsync(User.GetAdminId(), id, cancellationToken), "Refund marked as received."));
}
