using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>The signed-in driver's assigned bookings and the trip screen (start / end / complete). Another driver's booking is a 404.</summary>
[ApiController]
[Authorize(Roles = "Driver")]
[Route("api/driver")]
public class DriverBookingsController(ICarTripService tripService, ICarBookingService bookingService) : ControllerBase
{
    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DriverBookingDto>>>> List([FromQuery] DriverBookingScope scope = DriverBookingScope.Upcoming,
        CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<IReadOnlyList<DriverBookingDto>>.Ok(await tripService.ListForDriverAsync(User.GetDriverId(), scope, cancellationToken)));

    [HttpGet("bookings/{id:int}")]
    public async Task<ActionResult<ApiResponse<DriverBookingDto>>> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverBookingDto>.Ok(await tripService.GetForDriverAsync(User.GetDriverId(), id, cancellationToken)));

    [HttpPost("bookings/{id:int}/start")]
    public async Task<ActionResult<ApiResponse<DriverBookingDto>>> Start(int id, StartTripRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverBookingDto>.Ok(await tripService.StartTripAsync(User.GetDriverId(), id, request, cancellationToken), "Trip started."));

    /// <summary>Final fare for the entered End KM / night halts / extras — calculated by the server, nothing saved.</summary>
    [HttpPost("bookings/{id:int}/end/preview")]
    public async Task<ActionResult<ApiResponse<TripFarePreviewDto>>> PreviewEnd(int id, EndTripRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<TripFarePreviewDto>.Ok(await tripService.PreviewEndAsync(User.GetDriverId(), id, request, cancellationToken)));

    [HttpPost("bookings/{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<DriverBookingDto>>> Complete(int id, EndTripRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverBookingDto>.Ok(await tripService.CompleteTripAsync(User.GetDriverId(), id, request, cancellationToken), "Trip completed."));

    [HttpPost("bookings/{id:int}/balance-collected")]
    public async Task<ActionResult<ApiResponse<DriverBookingDto>>> BalanceCollected(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverBookingDto>.Ok(await tripService.MarkBalanceCollectedAsync(User.GetDriverId(), id, cancellationToken), "Balance marked as collected."));

    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<DriverBookingDto>>> Cancel(int id, CancelCarBookingRequest request, CancellationToken cancellationToken)
    {
        var driverId = User.GetDriverId();
        await bookingService.CancelByDriverAsync(driverId, id, request.Reason, cancellationToken);
        return Ok(ApiResponse<DriverBookingDto>.Ok(await tripService.GetForDriverAsync(driverId, id, cancellationToken), "Booking cancelled. The customer has been informed on their booking page."));
    }

    [HttpGet("earnings")]
    public async Task<ActionResult<ApiResponse<DriverEarningsDto>>> Earnings(CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverEarningsDto>.Ok(await tripService.GetEarningsAsync(User.GetDriverId(), cancellationToken)));
}
