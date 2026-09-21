using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Payments;

/// <summary>
/// Orchestrates Razorpay order creation and payment verification for a customer's own booking.
/// Verified payments are confirmed through <see cref="IBookingService.ConfirmBookingAsync"/> — the
/// same guarded, transactional seat-deduction path the admin's manual Confirm button uses — so this
/// never opens a second way to decrement <c>AvailableSeats</c>.
/// </summary>
public class BookingPaymentService(
    IGhumoOdishaDbContext db,
    IRazorpayService razorpay,
    IBookingService bookingService,
    ICouponService couponService,
    IOptions<RazorpayOptions> razorpayOptions,
    ILogger<BookingPaymentService> logger) : IBookingPaymentService
{
    // Business rule, not a secret: reserve a seat for ₹99/seat now (balance collected later, same as
    // an offline advance), or skip the balance entirely by paying ₹199/seat less up front.
    private const decimal PerSeatAdvanceAmount = 99m;
    private const decimal FullPaymentDiscountPerSeat = 199m;

    private readonly RazorpayOptions _razorpayOptions = razorpayOptions.Value;

    public async Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int bookingId, BookingPaymentPlan plan, string? couponCode, CancellationToken cancellationToken = default)
    {
        var booking = await LoadOwnedPayableBookingAsync(customerId, bookingId, cancellationToken);

        int? couponCodeId = null;
        var couponDiscount = 0m;
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var validation = await couponService.ValidateAsync(couponCode, customerId, cancellationToken);
            couponCodeId = validation.CouponCodeId;
            couponDiscount = validation.DiscountAmount;
        }

        var (advanceAmount, totalDiscount) = ComputeAmounts(booking, plan, couponDiscount);
        var amountPaise = ToPaise(advanceAmount);

        var order = await razorpay.CreateOrderAsync(amountPaise, $"GO-{booking.BookingId}-{(int)plan}", cancellationToken);

        booking.RazorpayOrderId = order.Id;
        booking.PendingAdvanceAmount = advanceAmount;
        booking.PendingDiscountAmount = totalDiscount;
        booking.PendingCouponCodeId = couponCodeId;
        booking.PendingCouponDiscountAmount = couponCodeId.HasValue ? couponDiscount : null;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new CreatePaymentOrderResult(order.Id, order.AmountPaise, order.Currency, _razorpayOptions.KeyId, totalDiscount);
    }

    public async Task VerifyAndConfirmAsync(int customerId, int bookingId, VerifyPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await LoadOwnedPayableBookingAsync(customerId, bookingId, cancellationToken);

        if (string.IsNullOrEmpty(booking.RazorpayOrderId) || booking.RazorpayOrderId != request.RazorpayOrderId)
        {
            throw new PaymentVerificationException();
        }

        if (!razorpay.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature))
        {
            throw new PaymentVerificationException();
        }

        // Never recomputed from anything the client sends here — exactly what CreateOrderAsync
        // already quoted and charged via Razorpay.
        var advanceAmount = booking.PendingAdvanceAmount ?? throw new PaymentVerificationException();
        var discountAmount = booking.PendingDiscountAmount ?? 0m;
        var couponCodeId = booking.PendingCouponCodeId;
        var couponDiscount = booking.PendingCouponDiscountAmount ?? 0m;

        await bookingService.ConfirmBookingAsync(
            bookingId,
            new ConfirmBookingRequest(advanceAmount, discountAmount, request.RazorpayPaymentId),
            cancellationToken);

        if (couponCodeId.HasValue)
        {
            try
            {
                await couponService.RedeemAsync(couponCodeId.Value, customerId, bookingId, couponDiscount, cancellationToken);
            }
            catch (ConflictException ex)
            {
                // The booking is already confirmed and paid at this point — a redemption-record race
                // (the same coupon redeemed twice, extremely narrow window) can't undo that charge.
                // Log for manual follow-up rather than failing an otherwise-successful confirmation.
                logger.LogWarning(ex, "Coupon redemption bookkeeping failed for booking {BookingId}, coupon {CouponCodeId} — booking is confirmed regardless.", bookingId, couponCodeId);
            }
        }
    }

    private async Task<Booking> LoadOwnedPayableBookingAsync(int customerId, int bookingId, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings
            .Include(b => b.Trip)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        if (booking.BookingStatus is not (BookingStatus.Requested or BookingStatus.Pending))
        {
            throw new ConflictException("This booking can no longer be paid for online.");
        }

        return booking;
    }

    private (decimal AdvanceAmount, decimal TotalDiscount) ComputeAmounts(Booking booking, BookingPaymentPlan plan, decimal couponDiscount)
    {
        var totalAmount = booking.Trip.AmountPerPerson * booking.NumberOfSeats;
        var planDiscount = plan == BookingPaymentPlan.Full ? FullPaymentDiscountPerSeat * booking.NumberOfSeats : 0m;
        var totalDiscount = planDiscount + couponDiscount;
        var effectiveTotal = Math.Max(0, totalAmount - totalDiscount);

        var advanceAmount = plan switch
        {
            // The coupon discounts the trip total / balance, not the small reservation fee itself —
            // otherwise a 1-seat booking with a ₹199 coupon could push the "pay now" amount below zero.
            BookingPaymentPlan.Partial => Math.Min(PerSeatAdvanceAmount * booking.NumberOfSeats, effectiveTotal),
            BookingPaymentPlan.Full => effectiveTotal,
            _ => throw new ValidationAppException(["Unknown payment plan."])
        };

        return (advanceAmount, totalDiscount);
    }

    private static long ToPaise(decimal rupees) => (long)Math.Round(rupees * 100m, MidpointRounding.AwayFromZero);
}
