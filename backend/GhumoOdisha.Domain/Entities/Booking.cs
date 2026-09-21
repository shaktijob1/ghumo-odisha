using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

public class Booking
{
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public int TripId { get; set; }
    public int TripDateSlotId { get; set; }
    public int? PickupPointId { get; set; }
    public int NumberOfSeats { get; set; }
    public Guid? ClientRequestId { get; set; }
    public decimal AmountPerPerson { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AdvanceAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public BookingSource BookingSource { get; set; }
    public string? CustomerNotes { get; set; }
    public string? AdminNotes { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }

    // Set by BookingPaymentService.CreateOrderAsync, consumed by VerifyAndConfirmAsync — so a
    // verified payment always confirms exactly what was quoted/charged at order-creation time,
    // never a plan/coupon re-supplied (and possibly altered) by the client at verify time.
    public decimal? PendingAdvanceAmount { get; set; }
    public decimal? PendingDiscountAmount { get; set; }
    public int? PendingCouponCodeId { get; set; }
    public decimal? PendingCouponDiscountAmount { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
    public TripDateSlot TripDateSlot { get; set; } = null!;
    public PickupPoint? PickupPoint { get; set; }
}
