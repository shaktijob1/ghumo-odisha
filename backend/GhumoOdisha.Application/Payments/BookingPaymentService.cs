using System.Data;
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

    private const string DevOrderPrefix = "dev_order_";

    private readonly RazorpayOptions _razorpayOptions = razorpayOptions.Value;

    public async Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int bookingId, BookingPaymentPlan plan, string? couponCode, CancellationToken cancellationToken = default)
    {
        int? couponCodeId = null;
        var couponDiscount = 0m;
        if (!string.IsNullOrWhiteSpace(couponCode))
        {
            var validation = await couponService.ValidateAsync(couponCode, customerId, cancellationToken);
            couponCodeId = validation.CouponCodeId;
            couponDiscount = validation.DiscountAmount;
        }

        // FOR UPDATE serializes concurrent create-order calls for the same booking (a background
        // prefetch racing a second tab or a page reload mid-request) so two calls can never both
        // read "no matching pending order" and each mint a separate Razorpay order — whichever
        // transaction gets the row lock first decides the order; the second one, once unblocked,
        // re-reads that committed order and reuses it via the idempotency check below instead of
        // creating a second one and orphaning whichever order the customer actually pays against.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var booking = await db.Bookings
                .FromSqlInterpolated($"SELECT * FROM Bookings WHERE BookingId = {bookingId} FOR UPDATE")
                .SingleOrDefaultAsync(b => b.CustomerId == customerId, cancellationToken)
                ?? throw new NotFoundException("Booking not found.");

            if (booking.BookingStatus is not (BookingStatus.Requested or BookingStatus.Pending))
            {
                throw new ConflictException("This booking can no longer be paid for online.");
            }

            var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.TripId == booking.TripId, cancellationToken)
                ?? throw new NotFoundException("Trip not found.");

            // Never take money for a departure that can't be confirmed — a full, hidden or departed
            // slot is rejected here, before a Razorpay order even exists.
            var slot = await db.TripDateSlots.AsNoTracking().FirstOrDefaultAsync(s => s.TripDateSlotId == booking.TripDateSlotId, cancellationToken)
                ?? throw new NotFoundException("Date slot not found.");
            SlotBookingRules.EnsureBookable(slot, booking.NumberOfSeats);

            var (advanceAmount, totalDiscount) = ComputeAmounts(booking, trip, plan, couponDiscount);
            var amountPaise = ToPaise(advanceAmount);

            // Reuse the already-pending Razorpay order when nothing about the charge has changed,
            // instead of minting a fresh one on every call (background prefetch, plan/coupon
            // re-selection, a retried click).
            if (!string.IsNullOrEmpty(booking.RazorpayOrderId)
                && booking.RazorpayOrderId.StartsWith(DevOrderPrefix, StringComparison.Ordinal) == _razorpayOptions.DevBypassEnabled
                && booking.PendingAdvanceAmount == advanceAmount
                && booking.PendingDiscountAmount == totalDiscount
                && booking.PendingCouponCodeId == couponCodeId)
            {
                await transaction.CommitAsync(cancellationToken);
                return new CreatePaymentOrderResult(booking.RazorpayOrderId, amountPaise, "INR", _razorpayOptions.KeyId, totalDiscount, _razorpayOptions.DevBypassEnabled);
            }

            var order = _razorpayOptions.DevBypassEnabled
                ? new RazorpayOrder($"{DevOrderPrefix}{Guid.NewGuid():N}", amountPaise, "INR")
                : await razorpay.CreateOrderAsync(amountPaise, $"GO-{booking.BookingId}-{(int)plan}", cancellationToken);

            booking.RazorpayOrderId = order.Id;
            booking.PendingAdvanceAmount = advanceAmount;
            booking.PendingDiscountAmount = totalDiscount;
            booking.PendingCouponCodeId = couponCodeId;
            booking.PendingCouponDiscountAmount = couponCodeId.HasValue ? couponDiscount : null;
            booking.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new CreatePaymentOrderResult(order.Id, order.AmountPaise, order.Currency, _razorpayOptions.KeyId, totalDiscount, _razorpayOptions.DevBypassEnabled);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task VerifyAndConfirmAsync(int customerId, int bookingId, VerifyPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await LoadOwnedPayableBookingAsync(customerId, bookingId, cancellationToken);

        // TEMP (diagnostics): safe to remove once the order-id-mismatch race is confirmed fixed.
        // Never logs the signature value or the Razorpay key secret.
        logger.LogInformation(
            "Payment verify attempt: booking {BookingId}, status {BookingStatus}, storedOrderId {StoredOrderId}, requestOrderId {RequestOrderId}, paymentId {PaymentId}, signaturePresent {SignaturePresent}, pendingAdvanceAmount {PendingAdvanceAmount}",
            bookingId, booking.BookingStatus, booking.RazorpayOrderId, request.RazorpayOrderId, request.RazorpayPaymentId,
            !string.IsNullOrWhiteSpace(request.RazorpaySignature), booking.PendingAdvanceAmount);

        if (string.IsNullOrEmpty(booking.RazorpayOrderId) || booking.RazorpayOrderId != request.RazorpayOrderId)
        {
            logger.LogWarning(
                "Payment verify FAILED — order id mismatch: booking {BookingId}, stored {StoredOrderId}, request {RequestOrderId}.",
                bookingId, booking.RazorpayOrderId, request.RazorpayOrderId);
            throw new PaymentVerificationException();
        }

        // Dev bypass: the order id was minted locally in CreateOrderAsync and never reached Razorpay,
        // so there is no signature to check. The prefix check keeps real orders on the real check.
        var devBypassPayment = _razorpayOptions.DevBypassEnabled && request.RazorpayOrderId.StartsWith(DevOrderPrefix, StringComparison.Ordinal);
        var signatureMatches = devBypassPayment
            || razorpay.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature);
        logger.LogInformation("Payment verify signature check: booking {BookingId}, matches {SignatureMatches}", bookingId, signatureMatches);
        if (!signatureMatches)
        {
            logger.LogWarning(
                "Payment verify FAILED — signature mismatch: booking {BookingId}, orderId {OrderId}, paymentId {PaymentId}.",
                bookingId, request.RazorpayOrderId, request.RazorpayPaymentId);
            throw new PaymentVerificationException();
        }

        // Never recomputed from anything the client sends here — exactly what CreateOrderAsync
        // already quoted and charged via Razorpay.
        if (booking.PendingAdvanceAmount is null)
        {
            logger.LogWarning("Payment verify FAILED — no pending advance amount recorded: booking {BookingId}.", bookingId);
            throw new PaymentVerificationException();
        }
        var advanceAmount = booking.PendingAdvanceAmount.Value;
        var discountAmount = booking.PendingDiscountAmount ?? 0m;
        logger.LogInformation(
            "Payment verify OK: booking {BookingId}, advanceAmount {AdvanceAmount}, discountAmount {DiscountAmount}. Confirming booking.",
            bookingId, advanceAmount, discountAmount);
        var couponCodeId = booking.PendingCouponCodeId;
        var couponDiscount = booking.PendingCouponDiscountAmount ?? 0m;

        try
        {
            await bookingService.ConfirmBookingAsync(
                bookingId,
                // No payment id for a dev-bypassed payment — otherwise a later cancel would try to refund
                // a Razorpay payment that doesn't exist and block the cancellation.
                new ConfirmBookingRequest(advanceAmount, discountAmount, devBypassPayment ? null : request.RazorpayPaymentId,
                    PaymentMethod.Razorpay, devBypassPayment ? "dev-bypass" : null),
                cancellationToken);
        }
        // Only seat/departure failures — never a status conflict (e.g. a duplicate verify racing the
        // first one), which would otherwise refund a payment for a booking that did get confirmed.
        catch (Exception ex) when (ex is InsufficientSeatsException or DepartureClosedException)
        {
            // The slot filled up (or closed) between order creation and payment — the money was
            // captured but the seats can't be given, so hand it straight back rather than keeping it.
            await RefundUnconfirmablePaymentAsync(bookingId, devBypassPayment ? null : request.RazorpayPaymentId, cancellationToken);
            throw new ConflictException("Sorry — these seats were taken while you were paying. Your payment has been refunded in full.");
        }

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

    public async Task<string?> GetRefundStatusAsync(int customerId, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        if (string.IsNullOrEmpty(booking.RazorpayRefundId))
        {
            return null;
        }

        return await razorpay.GetRefundStatusAsync(booking.RazorpayRefundId, cancellationToken);
    }

    private async Task RefundUnconfirmablePaymentAsync(int bookingId, string? razorpayPaymentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(razorpayPaymentId))
        {
            return;
        }

        try
        {
            var refundId = await razorpay.RefundAsync(razorpayPaymentId, cancellationToken);
            logger.LogWarning("Booking {BookingId} could not be confirmed after payment {PaymentId} — refunded as {RefundId}.",
                bookingId, razorpayPaymentId, refundId);
        }
        catch (Exception ex)
        {
            // Nothing more can be done automatically — this needs a manual refund from the Razorpay dashboard.
            logger.LogError(ex, "Booking {BookingId} could not be confirmed and the automatic refund of payment {PaymentId} FAILED — refund manually.",
                bookingId, razorpayPaymentId);
            throw new ConflictException("These seats were taken while you were paying. We couldn't refund you automatically — please contact us and we'll refund you right away.");
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

    private (decimal AdvanceAmount, decimal TotalDiscount) ComputeAmounts(Booking booking, Trip trip, BookingPaymentPlan plan, decimal couponDiscount)
    {
        var totalAmount = trip.AmountPerPerson * booking.NumberOfSeats;
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
