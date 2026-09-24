using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Application.Payments;

public interface IRazorpayService
{
    Task<RazorpayOrder> CreateOrderAsync(long amountPaise, string receipt, CancellationToken cancellationToken = default);

    bool VerifySignature(string orderId, string paymentId, string signature);

    /// <summary>Refunds the full amount originally captured for this payment; returns the created refund's id.</summary>
    Task<string> RefundAsync(string paymentId, CancellationToken cancellationToken = default);

    /// <summary>Live status of a previously created refund, exactly as Razorpay reports it
    /// (e.g. "pending", "processed", "failed") — never cached/assumed locally.</summary>
    Task<string> GetRefundStatusAsync(string refundId, CancellationToken cancellationToken = default);
}
