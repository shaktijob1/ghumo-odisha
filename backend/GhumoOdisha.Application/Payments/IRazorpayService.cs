using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Application.Payments;

public interface IRazorpayService
{
    Task<RazorpayOrder> CreateOrderAsync(long amountPaise, string receipt, CancellationToken cancellationToken = default);

    bool VerifySignature(string orderId, string paymentId, string signature);

    /// <summary>Refunds the full amount originally captured for this payment.</summary>
    Task RefundAsync(string paymentId, CancellationToken cancellationToken = default);
}
