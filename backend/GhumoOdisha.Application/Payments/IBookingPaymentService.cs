using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Application.Payments;

public interface IBookingPaymentService
{
    Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int bookingId, BookingPaymentPlan plan, string? couponCode, CancellationToken cancellationToken = default);

    Task VerifyAndConfirmAsync(int customerId, int bookingId, VerifyPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Live refund status straight from Razorpay for this booking's refund, or null if
    /// no online refund was ever issued for it (never paid online, or refunded manually/offline).</summary>
    Task<string?> GetRefundStatusAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);
}
