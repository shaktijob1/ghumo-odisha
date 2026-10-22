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

    /// <summary>
    /// "Did my payment go through?" — after a refresh, or when the browser couldn't report the payment.
    /// Asks Razorpay and confirms the booking if it was paid.
    /// </summary>
    [HttpPost("api/customer/bookings/{id:int}/payments/check")]
    public async Task<ActionResult<ApiResponse<PaymentCheckResult>>> CheckPayment(int id, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await paymentService.CheckPaymentAsync(customerId, id, cancellationToken);
        return Ok(ApiResponse<PaymentCheckResult>.Ok(result));
    }

    [HttpGet("api/customer/bookings/{id:int}/payments/refund-status")]
    public async Task<ActionResult<ApiResponse<object>>> GetRefundStatus(int id, CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var status = await paymentService.GetRefundStatusAsync(customerId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { status }));
    }
}
