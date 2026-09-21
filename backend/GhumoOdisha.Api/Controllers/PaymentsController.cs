using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
public class PaymentsController(IBookingPaymentService paymentService) : ControllerBase
{
    [HttpPost("api/customer/bookings/{id:int}/payments/order")]
    public async Task<ActionResult<ApiResponse<CreatePaymentOrderResult>>> CreateOrder(
        int id, CreatePaymentOrderRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await paymentService.CreateOrderAsync(customerId, id, request.Plan, request.CouponCode, cancellationToken);
        return Ok(ApiResponse<CreatePaymentOrderResult>.Ok(result));
    }

    [HttpPost("api/customer/bookings/{id:int}/payments/verify")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyPayment(
        int id, VerifyPaymentRequest request, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        await paymentService.VerifyAndConfirmAsync(customerId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Payment verified. Booking confirmed."));
    }
}
