using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>Stands in for the real Razorpay HTTP calls in tests that exercise BookingService but
/// don't care about the payment gateway itself.</summary>
public class FakeRazorpayService : IRazorpayService
{
    public string? LastRefundedPaymentId { get; private set; }
    public int RefundCallCount { get; private set; }
    public bool ShouldFailRefund { get; set; }

    public Task<RazorpayOrder> CreateOrderAsync(long amountPaise, string receipt, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RazorpayOrder($"order_fake_{Guid.NewGuid():N}", amountPaise, "INR"));

    public bool VerifySignature(string orderId, string paymentId, string signature) => true;

    public Task RefundAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        if (ShouldFailRefund)
        {
            throw new PaymentGatewayException();
        }

        RefundCallCount++;
        LastRefundedPaymentId = paymentId;
        return Task.CompletedTask;
    }
}
