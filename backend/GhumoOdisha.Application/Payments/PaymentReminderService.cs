using System.Globalization;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Notifications;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Payments;

public interface IPaymentReminderService
{
    /// <summary>Sends every "complete your payment" reminder that's due now. Returns how many checkouts were reminded.</summary>
    Task<int> SendDueRemindersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// An unpaid website checkout stays open for <see cref="BookingService.UnpaidRequestExpiryMinutes"/>
/// minutes. The customer is reminded twice — <see cref="ReminderEveryMinutes"/> and twice that after
/// starting — on WhatsApp and by email, with a link straight back to the payment step.
/// Razorpay is checked (every minute, before this runs) so a customer who already paid isn't nagged.
/// </summary>
public class PaymentReminderService(
    IGhumoOdishaDbContext db,
    IWhatsAppService whatsApp,
    IBookingEmailService bookingEmail,
    IOptions<EmailOptions> emailOptions,
    ILogger<PaymentReminderService> logger) : IPaymentReminderService
{
    public const int MaxReminders = 2;
    public const int ReminderEveryMinutes = 15;

    /// <summary>No reminder this close to the expiry — it would only arrive as the request closes.</summary>
    private const int NoReminderInLastMinutes = 5;

    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notTooLate = now.AddMinutes(-(BookingService.UnpaidRequestExpiryMinutes - NoReminderInLastMinutes));
        var firstDue = now.AddMinutes(-ReminderEveryMinutes);

        var due = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .Where(b => (b.BookingStatus == BookingStatus.AwaitingPayment || b.BookingStatus == BookingStatus.Pending)
                     && b.BookingSource == BookingSource.Website
                     && b.AdvanceAmount == 0
                     && b.PaymentRemindersSent < MaxReminders
                     && b.RequestedAt <= firstDue
                     && b.RequestedAt > notTooLate)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var booking in due)
        {
            // Reminder n is due n × 15 minutes after the checkout started (15, then 30).
            if (booking.RequestedAt > now.AddMinutes(-ReminderEveryMinutes * (booking.PaymentRemindersSent + 1)))
            {
                continue;
            }

            // Counted before sending, so a slow or failing send is never repeated every minute.
            booking.PaymentRemindersSent++;
            booking.LastPaymentReminderAt = now;
            await db.SaveChangesAsync(cancellationToken);

            var minutesLeft = Math.Max(1, (int)Math.Ceiling((booking.RequestedAt.AddMinutes(BookingService.UnpaidRequestExpiryMinutes) - now).TotalMinutes));
            var amountDue = booking.PendingAdvanceAmount ?? BookingPaymentService.PerSeatAdvanceAmount * booking.NumberOfSeats;
            var payUrl = $"{emailOptions.Value.SiteUrl.TrimEnd('/')}/trips/{booking.TripId}?pay={booking.BookingId}";
            var date = booking.StartDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

            var whatsAppSent = false;
            if (!string.IsNullOrWhiteSpace(booking.Customer.PhoneNumber))
            {
                var name = string.IsNullOrWhiteSpace(booking.Customer.Name) ? "Traveller" : booking.Customer.Name.Trim();
                var message = $"reminder: your seat(s) for {booking.Trip.Title} on {date} are not confirmed yet. " +
                              $"Pay {BookingTimeline.Money(amountDue)} within {minutesLeft} minutes to keep your booking: {payUrl} " +
                              "(Already paid? It's matched automatically in a few minutes — please don't pay again.)";
                try
                {
                    await whatsApp.SendTemplateAsync(booking.Customer.PhoneNumber, name, message, cancellationToken);
                    whatsAppSent = true;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Payment reminder {Number} on WhatsApp failed for booking {BookingId}.", booking.PaymentRemindersSent, booking.BookingId);
                }
            }

            var emailSent = await bookingEmail.SendPaymentReminderAsync(booking.BookingId, amountDue, minutesLeft, payUrl, cancellationToken);

            BookingTimeline.Add(db, booking, BookingEventType.Requested, $"Payment reminder {booking.PaymentRemindersSent} sent",
                $"{(whatsAppSent ? "WhatsApp" : "")}{(whatsAppSent && emailSent ? " + " : "")}{(emailSent ? "email" : "")}{(!whatsAppSent && !emailSent ? "Could not be delivered" : "")} · {minutesLeft} min left",
                BookingTimeline.System, visibleToCustomer: false, at: now);
            await db.SaveChangesAsync(cancellationToken);
            sent++;
        }

        return sent;
    }
}
