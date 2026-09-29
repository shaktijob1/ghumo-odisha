using System.Data;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Cars;

public interface ICarBookingPaymentService
{
    Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int carBookingId, CancellationToken cancellationToken = default);
    Task<CarBookingDto> VerifyAsync(int customerId, int carBookingId, VerifyPaymentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The online booking amount (₹99 by default) for a car booking, through the same Razorpay integration
/// trip bookings use. The amount is the one stored on the booking at creation (from configuration) —
/// never anything the browser sends. A verified payment confirms the booking only after re-checking,
/// under the car lock, that nobody else took the car meanwhile; if they did, the payment is refunded.
/// </summary>
public class CarBookingPaymentService(
    IGhumoOdishaDbContext db,
    IRazorpayService razorpay,
    CarBookingService bookingService,
    IOptions<RazorpayOptions> razorpayOptions,
    IOptions<CarRentalOptions> carOptions,
    ILogger<CarBookingPaymentService> logger) : ICarBookingPaymentService
{
    private const string DevOrderPrefix = "dev_order_";
    private readonly RazorpayOptions _razorpay = razorpayOptions.Value;
    private readonly CarRentalOptions _options = carOptions.Value;

    public async Task<CreatePaymentOrderResult> CreateOrderAsync(int customerId, int carBookingId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CarBookingId FROM CarBookings WHERE CarBookingId = {carBookingId} FOR UPDATE", cancellationToken);

        var booking = await db.CarBookings.FirstOrDefaultAsync(b => b.CarBookingId == carBookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");
        if (booking.Status is not (CarBookingStatus.PendingPayment or CarBookingStatus.Expired))
        {
            throw new ConflictException(booking.Status == CarBookingStatus.Confirmed ? "This booking is already paid." : "This booking can no longer be paid for.");
        }

        var now = DateTime.UtcNow;
        if (booking.PickupAt <= now)
        {
            throw new ConflictException("The pickup time has passed. Please start a new booking.");
        }

        // Re-take the hold: an expired attempt can still go ahead if the car is free.
        await bookingService.LockCarAsync(booking.CarId, cancellationToken);
        if (!await db.Cars.Listed().AnyAsync(c => c.CarId == booking.CarId, cancellationToken))
        {
            throw new ConflictException("This car isn't available for booking any more.");
        }
        if (await db.CarBookings.Where(b => b.CarId == booking.CarId && b.CarBookingId != carBookingId)
                .Overlapping(booking.PickupAt, booking.EndsAt, _options.TurnaroundBufferMinutes, now).AnyAsync(cancellationToken))
        {
            throw new ConflictException("Sorry, this car was just booked for part of that time. Please pick another time or car.");
        }

        booking.Status = CarBookingStatus.PendingPayment;
        booking.HoldExpiresAt = now.AddMinutes(_options.PaymentHoldMinutes);
        booking.UpdatedAt = now;

        var amountPaise = (long)Math.Round(booking.BookingAmount * 100m);
        var reusable = !string.IsNullOrEmpty(booking.RazorpayOrderId)
            && booking.RazorpayOrderId.StartsWith(DevOrderPrefix, StringComparison.Ordinal) == _razorpay.DevBypassEnabled;
        if (!reusable)
        {
            var order = _razorpay.DevBypassEnabled
                ? new RazorpayOrder($"{DevOrderPrefix}{Guid.NewGuid():N}", amountPaise, "INR")
                : await razorpay.CreateOrderAsync(amountPaise, booking.Reference, cancellationToken);
            booking.RazorpayOrderId = order.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreatePaymentOrderResult(booking.RazorpayOrderId!, amountPaise, "INR", _razorpay.KeyId, 0m, _razorpay.DevBypassEnabled);
    }

    public async Task<CarBookingDto> VerifyAsync(int customerId, int carBookingId, VerifyPaymentRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT CarBookingId FROM CarBookings WHERE CarBookingId = {carBookingId} FOR UPDATE", cancellationToken);

        var booking = await db.CarBookings.FirstOrDefaultAsync(b => b.CarBookingId == carBookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        var devBypass = _razorpay.DevBypassEnabled && request.RazorpayOrderId.StartsWith(DevOrderPrefix, StringComparison.Ordinal);
        var paymentId = devBypass ? null : request.RazorpayPaymentId;

        // A retried verify for a payment that already confirmed the booking is a success, not an error.
        if (booking.Status == CarBookingStatus.Confirmed && booking.RazorpayOrderId == request.RazorpayOrderId)
        {
            await transaction.CommitAsync(cancellationToken);
            return await bookingService.GetForCustomerAsync(customerId, carBookingId, cancellationToken);
        }
        if (booking.Status is not (CarBookingStatus.PendingPayment or CarBookingStatus.Expired))
        {
            throw new ConflictException("This booking can no longer be paid for.");
        }
        if (string.IsNullOrEmpty(booking.RazorpayOrderId) || booking.RazorpayOrderId != request.RazorpayOrderId
            || !(devBypass || razorpay.VerifySignature(request.RazorpayOrderId, request.RazorpayPaymentId, request.RazorpaySignature)))
        {
            logger.LogWarning("Car booking payment verify failed: booking {CarBookingId}, order {OrderId}.", carBookingId, request.RazorpayOrderId);
            throw new PaymentVerificationException();
        }

        var confirmed = await bookingService.TryConfirmPaidAsync(booking, paymentId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (!confirmed)
        {
            await RefundUnconfirmableAsync(carBookingId, paymentId, cancellationToken);
            throw new ConflictException("Sorry — this car was booked by someone else while you were paying. Your payment has been refunded in full.");
        }

        return await bookingService.GetForCustomerAsync(customerId, carBookingId, cancellationToken);
    }

    /// <summary>Money was taken for a booking that can't exist — give it straight back (the one automatic refund).</summary>
    private async Task RefundUnconfirmableAsync(int carBookingId, string? paymentId, CancellationToken cancellationToken)
    {
        if (paymentId is null)
        {
            // Dev bypass: nothing was actually charged.
            await MarkRefundedAsync(carBookingId, null, cancellationToken);
            return;
        }

        try
        {
            var refundId = await razorpay.RefundAsync(paymentId, cancellationToken: cancellationToken);
            await MarkRefundedAsync(carBookingId, refundId, cancellationToken);
            logger.LogWarning("Car booking {CarBookingId} could not be confirmed after payment {PaymentId} — refunded as {RefundId}.", carBookingId, paymentId, refundId);
        }
        catch (Exception ex)
        {
            // Stays RefundPending in the admin's queue.
            logger.LogError(ex, "Car booking {CarBookingId}: automatic refund of payment {PaymentId} FAILED — refund manually.", carBookingId, paymentId);
            throw new ConflictException("This car was booked by someone else while you were paying. We couldn't refund you automatically — please contact us and we'll refund you right away.");
        }
    }

    private async Task MarkRefundedAsync(int carBookingId, string? refundId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await db.CarBookings.Where(b => b.CarBookingId == carBookingId && b.PaymentStatus == CarPaymentStatus.RefundPending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.PaymentStatus, CarPaymentStatus.Refunded)
                .SetProperty(b => b.RefundMethod, (PaymentMethod?)PaymentMethod.Razorpay)
                .SetProperty(b => b.RazorpayRefundId, refundId)
                .SetProperty(b => b.RefundIssuedAt, now)
                .SetProperty(b => b.RefundSettledAt, now), cancellationToken);
    }
}
