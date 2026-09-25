using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// One amount actually received against a booking — the online advance, then every cash/UPI/bank
/// instalment the admin records later. Booking.AdvanceAmount is kept equal to the sum of these rows
/// (it's the "amount paid so far"), updated in the same transaction that adds or removes a row.
/// </summary>
public class BookingPayment
{
    public int BookingPaymentId { get; set; }
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    /// <summary>Razorpay payment id, UPI/bank UTR, receipt number — whatever traces the money.</summary>
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>"Customer" for online payments the customer made, "Admin" for amounts recorded by the organizer.</summary>
    public string RecordedBy { get; set; } = null!;
    public DateTime PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
}
