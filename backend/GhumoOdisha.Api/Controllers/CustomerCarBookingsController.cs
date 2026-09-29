using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>The signed-in customer's own car bookings. The customer id always comes from the JWT.</summary>
[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/customer/car-bookings")]
public class CustomerCarBookingsController(ICarBookingService bookingService, ICarBookingPaymentService paymentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<CarBookingDto>>> Create(CreateCarBookingRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarBookingDto>.Ok(await bookingService.CreateAsync(User.GetCustomerId(), request, cancellationToken), "Booking started. Pay to confirm."));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CarBookingDto>>>> List(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CarBookingDto>>.Ok(await bookingService.ListForCustomerAsync(User.GetCustomerId(), cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CarBookingDto>>> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarBookingDto>.Ok(await bookingService.GetForCustomerAsync(User.GetCustomerId(), id, cancellationToken)));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<CarBookingDto>>> Cancel(int id, CancelCarBookingRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarBookingDto>.Ok(await bookingService.CancelByCustomerAsync(User.GetCustomerId(), id, request.Reason, cancellationToken), "Booking cancelled."));

    [HttpPost("{id:int}/payments/order")]
    public async Task<ActionResult<ApiResponse<CreatePaymentOrderResult>>> CreateOrder(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CreatePaymentOrderResult>.Ok(await paymentService.CreateOrderAsync(User.GetCustomerId(), id, cancellationToken)));

    [HttpPost("{id:int}/payments/verify")]
    public async Task<ActionResult<ApiResponse<CarBookingDto>>> Verify(int id, VerifyPaymentRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarBookingDto>.Ok(await paymentService.VerifyAsync(User.GetCustomerId(), id, request, cancellationToken), "Payment received. Booking confirmed."));
}
