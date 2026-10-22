using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Application.Payments;

public interface IRazorpayService
{
    Task<RazorpayOrder> CreateOrderAsync(long amountPaise, string receipt, CancellationToken cancellationToken = default);

    bool VerifySignature(string orderId, string paymentId, string signature);

    /// <summary>Every payment attempt on an order, live from Razorpay (GET /v1/orders/{id}/payments).</summary>
    Task<IReadOnlyList<RazorpayPayment>> GetOrderPaymentsAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>Captures an authorized payment for exactly <paramref name="amountPaise"/> (accounts without auto-capture).</summary>
    Task<RazorpayPayment> CapturePaymentAsync(string paymentId, long amountPaise, CancellationToken cancellationToken = default);

    /// <summary>Refunds this payment — the full captured amount, or just <paramref name="amountPaise"/> when given
    /// (a partial refund). Returns the created refund's id.</summary>
    Task<string> RefundAsync(string paymentId, long? amountPaise = null, CancellationToken cancellationToken = default);

    /// <summary>Live status of a previously created refund, exactly as Razorpay reports it
    /// (e.g. "pending", "processed", "failed") — never cached/assumed locally.</summary>
    Task<string> GetRefundStatusAsync(string refundId, CancellationToken cancellationToken = default);
}
