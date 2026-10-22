namespace GhumoOdisha.Application.Payments.Dtos;

/// <summary>Not persisted — only decides how much is charged now vs. left for the admin-tracked balance.</summary>
public enum BookingPaymentPlan
{
    Partial = 0,
    Full = 1
}

public record RazorpayOrder(string Id, long AmountPaise, string Currency);

/// <summary>A payment as Razorpay reports it. Status: created, authorized, captured, refunded or failed.</summary>
public record RazorpayPayment(string Id, string Status, long AmountPaise)
{
    public bool IsCaptured => Status == "captured";
    public bool IsAuthorized => Status == "authorized";
}

/// <summary>What the customer's "checking your payment" step learns from the server.</summary>
/// <param name="Status">One of <see cref="PaymentCheckStatus"/>.</param>
public record PaymentCheckResult(string Status, string? Message = null);

public static class PaymentCheckStatus
{
    /// <summary>The booking is paid and confirmed.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>No payment found yet — the checkout is still open.</summary>
    public const string Pending = "pending";

    /// <summary>The checkout has ended (expired, replaced or rejected); any payment on it is refunded.</summary>
    public const string Closed = "closed";
}

public record CreatePaymentOrderRequest(BookingPaymentPlan Plan, string? CouponCode = null);

public record CreatePaymentOrderResult(string OrderId, long AmountPaise, string Currency, string KeyId, decimal DiscountApplied, bool DevBypass = false);

/// <summary>No Plan/CouponCode here on purpose — verification confirms exactly what CreateOrderAsync
/// already quoted and locked onto the booking (Booking.PendingAdvanceAmount etc.), never a value
/// re-supplied by the client at this step.</summary>
public record VerifyPaymentRequest(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature);
