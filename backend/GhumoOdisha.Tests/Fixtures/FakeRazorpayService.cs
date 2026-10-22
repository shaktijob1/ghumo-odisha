using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>Stands in for the real Razorpay HTTP calls in tests that exercise BookingService but
/// don't care about the payment gateway itself.</summary>
public class FakeRazorpayService : IRazorpayService
{
    public string? LastRefundedPaymentId { get; private set; }
    public string? LastRefundId { get; private set; }
    public int RefundCallCount { get; private set; }
    public bool ShouldFailRefund { get; set; }
    public string RefundStatusToReturn { get; set; } = "processed";

    public Task<RazorpayOrder> CreateOrderAsync(long amountPaise, string receipt, CancellationToken cancellationToken = default) =>
        Task.FromResult(new RazorpayOrder($"order_fake_{Guid.NewGuid():N}", amountPaise, "INR"));

    public bool VerifySignature(string orderId, string paymentId, string signature) => true;

    public long? LastRefundAmountPaise { get; private set; }

    public Task<string> RefundAsync(string paymentId, long? amountPaise = null, CancellationToken cancellationToken = default)
    {
        if (ShouldFailRefund)
        {
            throw new PaymentGatewayException();
        }

        RefundCallCount++;
        LastRefundedPaymentId = paymentId;
        LastRefundAmountPaise = amountPaise;
        LastRefundId = $"rfnd_fake_{Guid.NewGuid():N}";
        return Task.FromResult(LastRefundId);
    }

    public Task<string> GetRefundStatusAsync(string refundId, CancellationToken cancellationToken = default) =>
        Task.FromResult(RefundStatusToReturn);

    /// <summary>Payments a test says were made on an order (as Razorpay would list them).</summary>
    public Dictionary<string, List<RazorpayPayment>> OrderPayments { get; } = new();
    public int CaptureCallCount { get; private set; }
    public int GetOrderPaymentsCallCount { get; private set; }

    /// <summary>Simulates the customer paying <paramref name="orderId"/> (captured unless <paramref name="authorizedOnly"/>).</summary>
    public string Pay(string orderId, long amountPaise, bool authorizedOnly = false)
    {
        var id = $"pay_fake_{Guid.NewGuid():N}"[..22];
        if (!OrderPayments.TryGetValue(orderId, out var list)) OrderPayments[orderId] = list = [];
        list.Add(new RazorpayPayment(id, authorizedOnly ? "authorized" : "captured", amountPaise));
        return id;
    }

    public Task<IReadOnlyList<RazorpayPayment>> GetOrderPaymentsAsync(string orderId, CancellationToken cancellationToken = default)
    {
        GetOrderPaymentsCallCount++;
        IReadOnlyList<RazorpayPayment> result = OrderPayments.TryGetValue(orderId, out var list)
            // A refunded payment reads as "refunded", like Razorpay.
            ? list.Select(p => p.Id == LastRefundedPaymentId ? p with { Status = "refunded" } : p).ToList()
            : [];
        return Task.FromResult(result);
    }

    public Task<RazorpayPayment> CapturePaymentAsync(string paymentId, long amountPaise, CancellationToken cancellationToken = default)
    {
        CaptureCallCount++;
        foreach (var list in OrderPayments.Values)
        {
            var i = list.FindIndex(p => p.Id == paymentId);
            if (i >= 0)
            {
                list[i] = list[i] with { Status = "captured" };
                return Task.FromResult(list[i]);
            }
        }

        throw new PaymentGatewayException();
    }
}
