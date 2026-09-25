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

    /// <summary>Everything knocked off AmountPerPerson x seats (coupon + plan discounts) at confirm
    /// time — kept so the total can be recomputed if the seat count changes later.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Amount paid so far — always the sum of <see cref="Payments"/>.</summary>
    public decimal AdvanceAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public BookingSource BookingSource { get; set; }
    /// <summary>How many of the seats are male / female travellers — null until the admin records it.</summary>
    public int? MaleCount { get; set; }
    public int? FemaleCount { get; set; }

    public string? CancellationReason { get; set; }

    /// <summary>Cancelled by the admin without refunding what was paid.</summary>
    public bool RefundWaived { get; set; }
    public string? CustomerNotes { get; set; }
    public string? AdminNotes { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string? RazorpayRefundId { get; set; }

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
    public ICollection<BookingPayment> Payments { get; set; } = new List<BookingPayment>();
    public ICollection<BookingTraveller> Travellers { get; set; } = new List<BookingTraveller>();
    public ICollection<BookingEvent> Events { get; set; } = new List<BookingEvent>();
}
