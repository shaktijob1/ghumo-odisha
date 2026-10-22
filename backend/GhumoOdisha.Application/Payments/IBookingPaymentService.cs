using GhumoOdisha.Application.Payments.Dtos;

namespace GhumoOdisha.Application.Payments;

public interface IBookingPaymentService
{
    Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int bookingId, BookingPaymentPlan plan, string? couponCode, CancellationToken cancellationToken = default);

    Task VerifyAndConfirmAsync(int customerId, int bookingId, VerifyPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Live refund status straight from Razorpay for this booking's refund, or null if
    /// no online refund was ever issued for it (never paid online, or refunded manually/offline).</summary>
    Task<string?> GetRefundStatusAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The customer's "did my payment go through?" check (after a refresh, or when the browser
    /// couldn't report the payment): asks Razorpay about the booking's orders and confirms it if paid.
    /// </summary>
    Task<PaymentCheckResult> CheckPaymentAsync(int customerId, int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks Razorpay about every order of this booking. An open checkout with a captured payment is
    /// confirmed (through the same ConfirmBookingAsync path as everything else); a payment that can't
    /// be used — the checkout closed, or the booking was already paid on another order — is refunded.
    /// </summary>
    Task<PaymentCheckResult> ReconcileAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Before a new request for the same date replaces the customer's unpaid checkouts there: was one
    /// of them actually paid (page refreshed right after paying)? Confirms it and returns its id, else null.
    /// </summary>
    Task<int?> ConfirmPaidCheckoutForSlotAsync(int customerId, int tripDateSlotId, CancellationToken cancellationToken = default);

    /// <summary>Background: <see cref="ReconcileAsync"/> for every unpaid website checkout that has a Razorpay order.</summary>
    Task<int> ReconcileOpenCheckoutsAsync(CancellationToken cancellationToken = default);

    /// <summary>Background: catches money that reached a closed checkout or a second order, and refunds it.</summary>
    Task<int> ReconcileClosedCheckoutsAsync(CancellationToken cancellationToken = default);
}
