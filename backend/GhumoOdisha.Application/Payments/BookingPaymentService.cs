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
    public const decimal PerSeatAdvanceAmount = 99m;
    public const decimal FullPaymentDiscountPerSeat = 199m;

    private const string DevOrderPrefix = "dev_order_";

    private readonly RazorpayOptions _razorpayOptions = razorpayOptions.Value;

    public const string AlreadyPaidMessage = "Your payment for this booking has already been received — your seats are confirmed.";

    public async Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int bookingId, BookingPaymentPlan plan, string? couponCode, CancellationToken cancellationToken = default)
    {
        // Back after a refresh with an order already paid? Find out from Razorpay before asking for
        // the money a second time — the reconciler confirms the booking from that payment.
        if (await db.BookingPaymentOrders.AnyAsync(o => o.BookingId == bookingId && o.Booking.CustomerId == customerId, cancellationToken)
            && (await ReconcileAsync(bookingId, cancellationToken)).Status == PaymentCheckStatus.Confirmed)
        {
            throw new ConflictException(AlreadyPaidMessage);
        }

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

            if (booking.BookingStatus is not (BookingStatus.AwaitingPayment or BookingStatus.Pending))
            {
                throw new ConflictException("This booking can no longer be paid for online.");
            }

            var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.TripId == booking.TripId, cancellationToken)
                ?? throw new NotFoundException("Trip not found.");

            // The organizer can switch coupons off per trip — refuse a code here rather than trust the UI to hide the field.
            if (couponCodeId is not null && !trip.AllowCoupons)
            {
                throw new ConflictException("Coupons are not applicable for this trip.");
            }

            // Never take money for a departure that can't be confirmed — a full, hidden or departed
            // slot is rejected here, before a Razorpay order even exists.
            var slot = await db.TripDateSlots.AsNoTracking().FirstOrDefaultAsync(s => s.TripDateSlotId == booking.TripDateSlotId, cancellationToken)
                ?? throw new NotFoundException("Date slot not found.");
            SlotBookingRules.EnsureBookable(trip, slot, booking.NumberOfSeats);

            // A coupon's amount is per seat: ₹200 off × 4 seats = ₹800 off the booking. Seats come from
            // the booking row, never from the client.
            couponDiscount *= booking.NumberOfSeats;
            var (advanceAmount, totalDiscount) = ComputeAmounts(booking, trip, slot, plan, couponDiscount);
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
                // Touched so the unpaid-request expiry leaves a customer who's on the payment screen alone.
                booking.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new CreatePaymentOrderResult(booking.RazorpayOrderId, amountPaise, "INR", _razorpayOptions.KeyId, totalDiscount, _razorpayOptions.DevBypassEnabled);
            }

            var order = _razorpayOptions.DevBypassEnabled
                ? new RazorpayOrder($"{DevOrderPrefix}{Guid.NewGuid():N}", amountPaise, "INR")
                : await razorpay.CreateOrderAsync(amountPaise, $"{booking.Reference}-{(int)plan}", cancellationToken);

            // Kept for good: a payment on this order is found and honoured even after a newer order replaces it.
            if (!_razorpayOptions.DevBypassEnabled)
            {
                db.BookingPaymentOrders.Add(new BookingPaymentOrder
                {
                    BookingId = booking.BookingId,
                    RazorpayOrderId = order.Id,
                    AmountPaise = order.AmountPaise,
                    AdvanceAmount = advanceAmount,
                    DiscountAmount = totalDiscount,
                    CouponCodeId = couponCodeId,
                    CouponDiscountAmount = couponCodeId.HasValue ? couponDiscount : null,
                    CreatedAt = DateTime.UtcNow,
                });
            }

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
        var booking = await db.Bookings
            .Include(b => b.Trip)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        // The reconciler (or a second tab) already confirmed it from this very payment: report success
        // instead of "can no longer be paid", which would scare a customer whose booking is done.
        if (booking.BookingStatus is BookingStatus.Confirmed or BookingStatus.Completed
            && !string.IsNullOrEmpty(booking.RazorpayPaymentId)
            && booking.RazorpayPaymentId == request.RazorpayPaymentId)
        {
            return;
        }

        // Which order is this payment for? Any order this booking ever had counts — a page refresh or a
        // plan/coupon change may have replaced it — and its own recorded amounts are what get confirmed.
        var order = await db.BookingPaymentOrders
            .FirstOrDefaultAsync(o => o.BookingId == bookingId && o.RazorpayOrderId == request.RazorpayOrderId, cancellationToken);

        if (booking.BookingStatus is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            await RefundPaymentOnClosedRequestAsync(booking, order, request, cancellationToken);
        }

        if (booking.BookingStatus is not (BookingStatus.AwaitingPayment or BookingStatus.Pending))
        {
            throw new ConflictException("This booking can no longer be paid for online.");
        }

        // Never logs the signature value or the Razorpay key secret.
        logger.LogInformation(
            "Payment verify attempt: booking {BookingId}, status {BookingStatus}, storedOrderId {StoredOrderId}, requestOrderId {RequestOrderId}, paymentId {PaymentId}, knownOrder {KnownOrder}",
            bookingId, booking.BookingStatus, booking.RazorpayOrderId, request.RazorpayOrderId, request.RazorpayPaymentId, order is not null);

        // Dev bypass: the order id was minted locally in CreateOrderAsync and never reached Razorpay,
        // so there is no signature to check. The prefix check keeps real orders on the real check.
        var devBypassPayment = _razorpayOptions.DevBypassEnabled && request.RazorpayOrderId.StartsWith(DevOrderPrefix, StringComparison.Ordinal);
        var legacyLatestOrder = order is null && !string.IsNullOrEmpty(booking.RazorpayOrderId) && booking.RazorpayOrderId == request.RazorpayOrderId;
        if (order is null && !legacyLatestOrder)
        {
            logger.LogWarning(
                "Payment verify FAILED — unknown order: booking {BookingId}, stored {StoredOrderId}, request {RequestOrderId}.",
                bookingId, booking.RazorpayOrderId, request.RazorpayOrderId);
            throw new PaymentVerificationException();
        }

        var signatureMatches = devBypassPayment
            || razorpay.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature);
        if (!signatureMatches)
        {
            logger.LogWarning(
                "Payment verify FAILED — signature mismatch: booking {BookingId}, orderId {OrderId}, paymentId {PaymentId}.",
                bookingId, request.RazorpayOrderId, request.RazorpayPaymentId);
            throw new PaymentVerificationException();
        }

        // Never recomputed from anything the client sends here — exactly what CreateOrderAsync quoted
        // for this order and Razorpay charged.
        var quote = order is not null
            ? new Quote(order.AdvanceAmount, order.DiscountAmount, order.CouponCodeId, order.CouponDiscountAmount ?? 0m)
            : booking.PendingAdvanceAmount is { } pending
                ? new Quote(pending, booking.PendingDiscountAmount ?? 0m, booking.PendingCouponCodeId, booking.PendingCouponDiscountAmount ?? 0m)
                : null;
        if (quote is null)
        {
            logger.LogWarning("Payment verify FAILED — no pending advance amount recorded: booking {BookingId}.", bookingId);
            throw new PaymentVerificationException();
        }

        logger.LogInformation("Payment verify OK: booking {BookingId}, advanceAmount {AdvanceAmount}. Confirming booking.", bookingId, quote.AdvanceAmount);
        var outcome = await ConfirmPaidAsync(bookingId, booking.CustomerId, quote, devBypassPayment ? null : request.RazorpayPaymentId, order, cancellationToken);
        if (outcome == ConfirmOutcome.SeatsGoneRefunded)
        {
            throw new ConflictException("Sorry — these seats were taken while you were paying. Your payment has been refunded in full.");
        }
        if (outcome == ConfirmOutcome.SeatsGoneRefundFailed)
        {
            throw new ConflictException("These seats were taken while you were paying. We couldn't refund you automatically — please contact us and we'll refund you right away.");
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

    // ---------- Reconciliation: Razorpay is the source of truth for "was it paid?" ----------

    /// <summary>Unpaid checkouts younger than this are checked every minute (they expire after 1 hour).</summary>
    private static readonly TimeSpan OpenCheckoutWindow = TimeSpan.FromHours(3);

    /// <summary>How long after its order was created a closed checkout is still watched for a late payment.</summary>
    private static readonly TimeSpan ClosedCheckoutWindow = TimeSpan.FromHours(48);

    /// <summary>Closed checkouts are checked this often, not every minute.</summary>
    private static readonly TimeSpan ClosedCheckInterval = TimeSpan.FromMinutes(10);

    /// <summary>A customer's "check my payment" taps within this time reuse the last answer from Razorpay.</summary>
    private static readonly TimeSpan CustomerCheckThrottle = TimeSpan.FromSeconds(5);

    // One reconcile at a time in this process, so the minute job and a customer's check can't both
    // confirm (or both refund) the same payment. The API runs as a single instance.
    private static readonly SemaphoreSlim ReconcileLock = new(1, 1);

    public async Task<PaymentCheckResult> CheckPaymentAsync(int customerId, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        var recentlyChecked = !await db.BookingPaymentOrders.AnyAsync(o => o.BookingId == bookingId
            && (o.LastCheckedAt == null || o.LastCheckedAt < DateTime.UtcNow - CustomerCheckThrottle), cancellationToken);
        return recentlyChecked ? StatusOf(booking) : await ReconcileAsync(bookingId, cancellationToken);
    }

    public async Task<PaymentCheckResult> ReconcileAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        await ReconcileLock.WaitAsync(cancellationToken);
        try
        {
            return await ReconcileLockedAsync(bookingId, cancellationToken);
        }
        finally
        {
            ReconcileLock.Release();
        }
    }

    public async Task<int?> ConfirmPaidCheckoutForSlotAsync(int customerId, int tripDateSlotId, CancellationToken cancellationToken = default)
    {
        var ids = await db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == customerId && b.TripDateSlotId == tripDateSlotId
                     && (b.BookingStatus == BookingStatus.AwaitingPayment || b.BookingStatus == BookingStatus.Pending)
                     && b.PaymentOrders.Any())
            .OrderByDescending(b => b.RequestedAt)
            .Select(b => b.BookingId)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            if ((await ReconcileAsync(id, cancellationToken)).Status == PaymentCheckStatus.Confirmed)
            {
                return id;
            }
        }

        return null;
    }

    public async Task<int> ReconcileOpenCheckoutsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow - OpenCheckoutWindow;
        var ids = await db.Bookings.AsNoTracking()
            .Where(b => (b.BookingStatus == BookingStatus.AwaitingPayment || b.BookingStatus == BookingStatus.Pending)
                     && b.RequestedAt > since
                     && b.PaymentOrders.Any())
            .Select(b => b.BookingId)
            .ToListAsync(cancellationToken);
        return await ReconcileEachAsync(ids, cancellationToken);
    }

    public async Task<int> ReconcileClosedCheckoutsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var createdSince = now - ClosedCheckoutWindow;
        var checkedBefore = now - ClosedCheckInterval;
        var ids = await db.BookingPaymentOrders.AsNoTracking()
            .Where(o => o.ConfirmedPaymentId == null && o.RefundedPaymentId == null
                     && o.CreatedAt > createdSince
                     && (o.LastCheckedAt == null || o.LastCheckedAt < checkedBefore)
                     && o.Booking.BookingStatus != BookingStatus.AwaitingPayment
                     && o.Booking.BookingStatus != BookingStatus.Pending)
            .Select(o => o.BookingId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return await ReconcileEachAsync(ids, cancellationToken);
    }

    /// <summary>Returns how many bookings were confirmed.</summary>
    private async Task<int> ReconcileEachAsync(IReadOnlyList<int> bookingIds, CancellationToken cancellationToken)
    {
        var confirmed = 0;
        foreach (var id in bookingIds)
        {
            try
            {
                var wasOpen = await db.Bookings.AsNoTracking().AnyAsync(b => b.BookingId == id
                    && (b.BookingStatus == BookingStatus.AwaitingPayment || b.BookingStatus == BookingStatus.Pending), cancellationToken);
                var result = await ReconcileAsync(id, cancellationToken);
                if (wasOpen && result.Status == PaymentCheckStatus.Confirmed) confirmed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad booking must not stop the rest from being checked.
                logger.LogError(ex, "Payment reconciliation failed for booking {BookingId}.", id);
            }
        }

        return confirmed;
    }

    private async Task<PaymentCheckResult> ReconcileLockedAsync(int bookingId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var booking = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
        if (booking is null)
        {
            return new PaymentCheckResult(PaymentCheckStatus.Closed);
        }

        var orders = await db.BookingPaymentOrders
            .Where(o => o.BookingId == bookingId)
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.BookingPaymentOrderId)
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            IReadOnlyList<RazorpayPayment> payments;
            try
            {
                payments = await razorpay.GetOrderPaymentsAsync(order.RazorpayOrderId, cancellationToken);
            }
            catch (Exception ex) when (ex is PaymentGatewayException or PaymentGatewayAuthException)
            {
                // Already logged by the gateway client; try again on the next run.
                continue;
            }

            order.LastCheckedAt = DateTime.UtcNow;

            foreach (var payment in payments.Where(p => (p.IsCaptured || p.IsAuthorized)
                         && p.Id != order.ConfirmedPaymentId && p.Id != order.RefundedPaymentId))
            {
                if (payment.AmountPaise != order.AmountPaise)
                {
                    // Never accept (or silently refund) an amount we didn't ask for — a person must look.
                    logger.LogError("Razorpay payment {PaymentId} on order {OrderId} (booking {BookingId}) is {Paid} paise, but the order was for {Quoted} — needs a manual check.",
                        payment.Id, order.RazorpayOrderId, bookingId, payment.AmountPaise, order.AmountPaise);
                    continue;
                }

                var captured = payment;
                if (payment.IsAuthorized)
                {
                    try
                    {
                        captured = await razorpay.CapturePaymentAsync(payment.Id, order.AmountPaise, cancellationToken);
                    }
                    catch (Exception ex) when (ex is PaymentGatewayException or PaymentGatewayAuthException)
                    {
                        continue;
                    }
                }

                if (!captured.IsCaptured)
                {
                    continue;
                }

                // Always the live row: the confirm below, a customer's verify or an admin action may have just changed it.
                booking = await db.Bookings.AsNoTracking().FirstAsync(b => b.BookingId == bookingId, cancellationToken);

                if (booking.BookingStatus is BookingStatus.AwaitingPayment or BookingStatus.Pending)
                {
                    var quote = new Quote(order.AdvanceAmount, order.DiscountAmount, order.CouponCodeId, order.CouponDiscountAmount ?? 0m);
                    var outcome = await ConfirmPaidAsync(bookingId, booking.CustomerId, quote, captured.Id, order, cancellationToken);
                    if (outcome == ConfirmOutcome.Confirmed)
                    {
                        logger.LogWarning("Booking {BookingId} confirmed from Razorpay payment {PaymentId} found by the reconciler (the browser never reported it).",
                            bookingId, captured.Id);
                        await AddNoteAsync(bookingId, "Payment found on Razorpay",
                            $"Payment {captured.Id} was received but not reported by the customer's browser (page refreshed or closed). Confirmed automatically.",
                            visibleToCustomer: false, cancellationToken);
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(booking.RazorpayPaymentId) && booking.RazorpayPaymentId == captured.Id)
                {
                    // This is the payment the booking was confirmed with — just note it on the order.
                    order.ConfirmedPaymentId = captured.Id;
                    await db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                // Money with nowhere to go: the checkout had closed, or the booking was already paid on another order.
                var why = booking.BookingStatus is BookingStatus.Confirmed or BookingStatus.Completed
                    ? "the booking was already paid"
                    : "this booking request had already closed";
                var refundId = await TryRefundAsync(bookingId, captured.Id, cancellationToken);
                if (refundId is not null)
                {
                    order.RefundedPaymentId = captured.Id;
                    order.RazorpayRefundId = refundId;
                    await db.SaveChangesAsync(cancellationToken);
                    await AddNoteAsync(bookingId, "Extra payment refunded",
                        $"A payment of {BookingTimeline.Money(order.AmountPaise / 100m)} arrived after {why}, so it was refunded in full automatically. It reaches your account in 5–7 working days.",
                        visibleToCustomer: true, cancellationToken);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var final = await db.Bookings.AsNoTracking().FirstAsync(b => b.BookingId == bookingId, cancellationToken);
        return StatusOf(final);
    }

    private static PaymentCheckResult StatusOf(Booking booking) => booking.BookingStatus switch
    {
        BookingStatus.Confirmed or BookingStatus.Completed => new PaymentCheckResult(PaymentCheckStatus.Confirmed),
        BookingStatus.AwaitingPayment or BookingStatus.Pending => new PaymentCheckResult(PaymentCheckStatus.Pending),
        _ => new PaymentCheckResult(PaymentCheckStatus.Closed, "This booking request has closed. Any payment made on it is refunded automatically."),
    };

    private sealed record Quote(decimal AdvanceAmount, decimal DiscountAmount, int? CouponCodeId, decimal CouponDiscount);

    private enum ConfirmOutcome { Confirmed, AlreadyConfirmed, SeatsGoneRefunded, SeatsGoneRefundFailed }

    /// <summary>
    /// Confirms a paid checkout through <see cref="IBookingService.ConfirmBookingAsync"/> — the one
    /// guarded, transactional seat-deduction path — then redeems its coupon. If the seats went while
    /// the customer was paying, the money goes straight back.
    /// </summary>
    private async Task<ConfirmOutcome> ConfirmPaidAsync(int bookingId, int customerId, Quote quote, string? razorpayPaymentId,
        BookingPaymentOrder? order, CancellationToken cancellationToken)
    {
        try
        {
            await bookingService.ConfirmBookingAsync(
                bookingId,
                // No payment id for a dev-bypassed payment — otherwise a later cancel would try to refund
                // a Razorpay payment that doesn't exist and block the cancellation.
                new ConfirmBookingRequest(quote.AdvanceAmount, quote.DiscountAmount, razorpayPaymentId,
                    PaymentMethod.Razorpay, razorpayPaymentId is null ? "dev-bypass" : null),
                cancellationToken);
        }
        // Only seat/departure failures — never a status conflict (e.g. a duplicate verify racing the
        // first one), which would otherwise refund a payment for a booking that did get confirmed.
        catch (Exception ex) when (ex is InsufficientSeatsException or DepartureClosedException)
        {
            if (razorpayPaymentId is null) return ConfirmOutcome.SeatsGoneRefunded;
            var refundId = await TryRefundAsync(bookingId, razorpayPaymentId, cancellationToken);
            if (refundId is null) return ConfirmOutcome.SeatsGoneRefundFailed;
            if (order is not null)
            {
                order.RefundedPaymentId = razorpayPaymentId;
                order.RazorpayRefundId = refundId;
                await db.SaveChangesAsync(cancellationToken);
            }
            return ConfirmOutcome.SeatsGoneRefunded;
        }
        catch (ConflictException) when (razorpayPaymentId is not null)
        {
            // Lost a race: someone else confirmed it first. Fine if it was with this very payment.
            var current = await db.Bookings.AsNoTracking().FirstAsync(b => b.BookingId == bookingId, cancellationToken);
            if (current.RazorpayPaymentId != razorpayPaymentId) throw;
            if (order is not null)
            {
                order.ConfirmedPaymentId = razorpayPaymentId;
                await db.SaveChangesAsync(cancellationToken);
            }
            return ConfirmOutcome.AlreadyConfirmed;
        }

        if (order is not null)
        {
            order.ConfirmedPaymentId = razorpayPaymentId;
            await db.SaveChangesAsync(cancellationToken);
        }

        if (quote.CouponCodeId.HasValue)
        {
            try
            {
                await couponService.RedeemAsync(quote.CouponCodeId.Value, customerId, bookingId, quote.CouponDiscount, cancellationToken);
            }
            catch (ConflictException ex)
            {
                // The booking is already confirmed and paid at this point — a redemption-record race
                // (the same coupon redeemed twice, extremely narrow window) can't undo that charge.
                logger.LogWarning(ex, "Coupon redemption bookkeeping failed for booking {BookingId}, coupon {CouponCodeId} — booking is confirmed regardless.", bookingId, quote.CouponCodeId);
            }
        }

        return ConfirmOutcome.Confirmed;
    }

    private async Task AddNoteAsync(int bookingId, string title, string description, bool visibleToCustomer, CancellationToken cancellationToken)
    {
        var tracked = await db.Bookings.FirstAsync(b => b.BookingId == bookingId, cancellationToken);
        BookingTimeline.Add(db, tracked, BookingEventType.PaymentReceived, title, description, BookingTimeline.System, visibleToCustomer);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The customer paid a Razorpay order for a request that was closed meanwhile — expired unpaid,
    /// replaced or rejected. There's no booking to put the money on, so it goes straight back.
    /// Only for a genuine, signed payment against one of this booking's own orders.
    /// </summary>
    private async Task RefundPaymentOnClosedRequestAsync(Booking booking, BookingPaymentOrder? order, VerifyPaymentRequest request, CancellationToken cancellationToken)
    {
        var knownOrder = order is not null
            || (!string.IsNullOrEmpty(booking.RazorpayOrderId) && booking.RazorpayOrderId == request.RazorpayOrderId);
        var genuine = knownOrder
            && !request.RazorpayOrderId.StartsWith(DevOrderPrefix, StringComparison.Ordinal)
            && razorpay.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature);
        if (!genuine)
        {
            return;
        }

        // The reconciler may already have refunded it.
        if (order?.RefundedPaymentId == request.RazorpayPaymentId)
        {
            throw new ConflictException("This booking request had already closed, so your payment has been refunded in full. Please book again.");
        }

        logger.LogWarning("Payment {PaymentId} arrived for booking {BookingId} after it was {Status} — refunding.",
            request.RazorpayPaymentId, booking.BookingId, booking.BookingStatus);
        var refundId = await TryRefundAsync(booking.BookingId, request.RazorpayPaymentId, cancellationToken)
            ?? throw new ConflictException("This booking request had already closed. We couldn't refund you automatically — please contact us and we'll refund you right away.");
        if (order is not null)
        {
            order.RefundedPaymentId = request.RazorpayPaymentId;
            order.RazorpayRefundId = refundId;
            await db.SaveChangesAsync(cancellationToken);
        }
        throw new ConflictException("This booking request had already closed, so your payment has been refunded in full. Please book again.");
    }

    /// <summary>Refunds the whole payment; returns the refund id, or null when the refund failed (logged for a manual refund).</summary>
    private async Task<string?> TryRefundAsync(int bookingId, string razorpayPaymentId, CancellationToken cancellationToken)
    {
        try
        {
            var refundId = await razorpay.RefundAsync(razorpayPaymentId, cancellationToken: cancellationToken);
            logger.LogWarning("Booking {BookingId}: payment {PaymentId} could not be used and was refunded as {RefundId}.",
                bookingId, razorpayPaymentId, refundId);
            return refundId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Nothing more can be done automatically — this needs a manual refund from the Razorpay dashboard.
            logger.LogError(ex, "Booking {BookingId}: the automatic refund of payment {PaymentId} FAILED — refund manually.",
                bookingId, razorpayPaymentId);
            return null;
        }
    }

    /// <summary><paramref name="couponDiscount"/> is the whole booking's coupon discount (per-seat amount × seats).</summary>
    private (decimal AdvanceAmount, decimal TotalDiscount) ComputeAmounts(Booking booking, Trip trip, TripDateSlot slot, BookingPaymentPlan plan, decimal couponDiscount)
    {
        var totalAmount = SlotPricing.PricePerPerson(trip, slot, booking) * booking.NumberOfSeats;
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
