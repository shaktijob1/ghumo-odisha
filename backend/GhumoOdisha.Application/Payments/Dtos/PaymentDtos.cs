namespace GhumoOdisha.Application.Payments.Dtos;

/// <summary>Not persisted — only decides how much is charged now vs. left for the admin-tracked balance.</summary>
public enum BookingPaymentPlan
{
    Partial = 0,
    Full = 1
}

public record RazorpayOrder(string Id, long AmountPaise, string Currency);

public record CreatePaymentOrderRequest(BookingPaymentPlan Plan, string? CouponCode = null);

public record CreatePaymentOrderResult(string OrderId, long AmountPaise, string Currency, string KeyId, decimal DiscountApplied);

/// <summary>No Plan/CouponCode here on purpose — verification confirms exactly what CreateOrderAsync
/// already quoted and locked onto the booking (Booking.PendingAdvanceAmount etc.), never a value
/// re-supplied by the client at this step.</summary>
public record VerifyPaymentRequest(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature);
