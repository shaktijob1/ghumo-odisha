using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Refunds.Dtos;

/// <summary>What the customer sees about their refund — no internal notes.</summary>
public record CustomerRefundDto(
    decimal Amount,
    RefundStatus Status,
    PaymentMethod? Method,
    string? Reference,
    DateTime RequestedAt,
    DateTime? InitiatedAt,
    DateTime? SettledAt);

public record AdminRefundDto(
    int RefundId,
    int BookingId,
    string BookingReference,
    decimal AmountPaid,
    decimal Amount,
    /// <summary>How much of what was paid went through Razorpay — the most a Razorpay refund can return.</summary>
    decimal OnlinePaidAmount,
    RefundStatus Status,
    PaymentMethod? Method,
    string? Reference,
    string? Notes,
    string RequestedBy,
    DateTime RequestedAt,
    DateTime? InitiatedAt,
    DateTime? SettledAt,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerEmail,
    string TripTitle,
    DateOnly StartDate,
    int NumberOfSeats,
    string? CancellationReason,
    DateTime? CancelledAt);

public record RefundCountsDto(int Pending, int Processing, int Settled);

/// <summary>Refund online: Razorpay returns the money to the customer's original payment method.</summary>
public record IssueRazorpayRefundRequest(decimal Amount, string? Notes);

/// <summary>Refund paid outside Razorpay (UPI, bank transfer, cash) — recorded with its reference.</summary>
public record RecordManualRefundRequest(decimal Amount, PaymentMethod Method, string? Reference, string? Notes);

public record SettleRefundRequest(string? Notes);
