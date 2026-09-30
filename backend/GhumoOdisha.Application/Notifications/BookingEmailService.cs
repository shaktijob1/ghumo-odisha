using System.Globalization;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Notifications;

public interface IBookingEmailService
{
    /// <summary>Best-effort: never throws — a failed email must not fail a booking confirmation.</summary>
    Task SendBookingConfirmedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer,
        BookingConfirmedWhatsAppMessage details, CancellationToken cancellationToken = default);

    /// <summary>Emails the invoice PDF of the customer's own booking to the address they typed.
    /// Throws (NotFound / EmailDelivery) so the caller can show what went wrong.</summary>
    Task SendInvoiceAsync(int customerId, int bookingId, string toEmail, CancellationToken cancellationToken = default);

    /// <summary>Best-effort: tells the customer their booking was cancelled and what happens to their money.</summary>
    Task SendBookingCancelledAsync(int bookingId, CancellationToken cancellationToken = default);

    /// <summary>Best-effort: receipt for a later payment (balance / instalment), with the updated invoice attached.</summary>
    Task SendPaymentReceivedAsync(int bookingId, decimal amount, string methodLabel, CancellationToken cancellationToken = default);
}

public class BookingEmailService(
    IEmailSender emailSender,
    IInvoiceService invoiceService,
    IGhumoOdishaDbContext db,
    IOptions<EmailOptions> emailOptions,
    ILogger<BookingEmailService> logger) : IBookingEmailService
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendInvoiceAsync(int customerId, int bookingId, string toEmail, CancellationToken cancellationToken = default)
    {
        if (!emailSender.IsConfigured)
        {
            throw new EmailDeliveryException("Email isn't available right now. Please download the invoice instead.");
        }

        // Same ownership rules as the invoice download — throws NotFound for anyone else's booking.
        var pdf = await invoiceService.GenerateInvoicePdfAsync(customerId, bookingId, cancellationToken);

        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .FirstAsync(b => b.BookingId == bookingId, cancellationToken);

        var (html, text) = EmailTemplates.Invoice(new EmailTemplates.InvoiceModel(
            string.IsNullOrWhiteSpace(booking.Customer.Name) ? "there" : booking.Customer.Name.Trim(),
            booking.Reference,
            booking.Trip.Title,
            $"{booking.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture)} – {booking.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}",
            $"{_options.SiteUrl.TrimEnd('/')}/my-bookings"));

        await emailSender.SendAsync(new EmailMessage(
            toEmail,
            booking.Customer.Name,
            $"Your invoice · {booking.Reference} · {booking.Trip.Title}",
            html,
            text,
            [new EmailAttachment($"Invoice-{booking.Reference}.pdf", "application/pdf", pdf)]), cancellationToken);

        logger.LogInformation("Invoice for booking {BookingId} emailed at the customer's request.", bookingId);
    }

    public async Task SendBookingConfirmedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer,
        BookingConfirmedWhatsAppMessage details, CancellationToken cancellationToken = default)
    {
        // Sent to whatever email is on file (verified or not) — it's the address the customer gave us.
        if (string.IsNullOrWhiteSpace(customer.Email) || !emailSender.IsConfigured)
        {
            return;
        }

        try
        {
            var attachments = new List<EmailAttachment>();
            try
            {
                var pdf = await invoiceService.GenerateAdminInvoicePdfAsync(booking.BookingId, cancellationToken);
                attachments.Add(new EmailAttachment($"Invoice-{booking.Reference}.pdf", "application/pdf", pdf));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Invoice PDF for booking {BookingId} could not be generated — sending the confirmation email without it.", booking.BookingId);
            }

            string Money(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

            var (html, text) = EmailTemplates.BookingConfirmed(new EmailTemplates.BookingConfirmedModel(
                CustomerName: details.CustomerName,
                BookingReference: details.BookingReference,
                TripTitle: trip.Title,
                TravelDates: $"{slot.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture)} – {slot.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}",
                Seats: booking.NumberOfSeats,
                TotalAmount: Money(booking.TotalAmount),
                AmountPaid: Money(booking.AdvanceAmount),
                Balance: Money(booking.RemainingAmount),
                PickupPoint: details.PickupPoint,
                ReportingTime: details.ReportingTime,
                MyBookingsUrl: $"{_options.SiteUrl.TrimEnd('/')}/my-bookings",
                InvoiceAttached: attachments.Count > 0));

            await emailSender.SendAsync(new EmailMessage(
                customer.Email,
                customer.Name,
                $"Booking confirmed · {details.BookingReference} · {trip.Title}",
                html,
                text,
                attachments), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking-confirmed email failed for booking {BookingId} — continuing without blocking the booking operation.", booking.BookingId);
        }
    }
public async Task SendBookingCancelledAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        try
        {
            var b = await LoadForEmailAsync(bookingId, cancellationToken);
            if (b is null) return;

            string refundLine;
            if (b.AdvanceAmount <= 0) refundLine = "No payment was made on this booking, so nothing is due back to you.";
            else if (b.RefundWaived) refundLine = $"As per our cancellation policy, the ₹{Money(b.AdvanceAmount)} paid is not refundable.";
            else refundLine = $"A refund of ₹{Money(b.AdvanceAmount)} has been requested. We'll let you know once it has been processed — it can then take a few working days to reach your account.";

            var (html, text) = EmailTemplates.BookingCancelled(new EmailTemplates.BookingCancelledModel(
                CustomerName: b.Customer.Name ?? "there",
                BookingReference: b.Reference,
                TripTitle: b.Trip.Title,
                TravelDates: TravelDates(b),
                Seats: b.NumberOfSeats,
                Reason: b.CancellationReason,
                RefundLine: refundLine,
                MyBookingsUrl: MyBookingsUrl));

            await emailSender.SendAsync(new EmailMessage(b.Customer.Email!, b.Customer.Name,
                $"Booking cancelled · {b.Reference} · {b.Trip.Title}", html, text), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Booking-cancelled email failed for booking {BookingId} — the cancellation itself stands.", bookingId);
        }
    }

    public async Task SendPaymentReceivedAsync(int bookingId, decimal amount, string methodLabel, CancellationToken cancellationToken = default)
    {
        try
        {
            var b = await LoadForEmailAsync(bookingId, cancellationToken);
            if (b is null) return;

            var attachments = new List<EmailAttachment>();
            try
            {
                var pdf = await invoiceService.GenerateAdminInvoicePdfAsync(b.BookingId, cancellationToken);
                attachments.Add(new EmailAttachment($"Invoice-{b.Reference}.pdf", "application/pdf", pdf));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Invoice PDF for booking {BookingId} could not be generated — sending the payment email without it.", b.BookingId);
            }

            var (html, text) = EmailTemplates.PaymentReceived(new EmailTemplates.PaymentReceivedModel(
                CustomerName: b.Customer.Name ?? "there",
                BookingReference: b.Reference,
                TripTitle: b.Trip.Title,
                TravelDates: TravelDates(b),
                AmountReceived: Money(amount),
                MethodLabel: methodLabel,
                TotalAmount: Money(b.TotalAmount),
                TotalPaid: Money(b.AdvanceAmount),
                Balance: Money(b.RemainingAmount),
                MyBookingsUrl: MyBookingsUrl,
                InvoiceAttached: attachments.Count > 0));

            await emailSender.SendAsync(new EmailMessage(b.Customer.Email!, b.Customer.Name,
                $"Payment received · ₹{Money(amount)} · {b.Reference}", html, text, attachments), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Payment-received email failed for booking {BookingId} — the payment itself is recorded.", bookingId);
        }
    }

    /// <summary>The booking with its trip and customer, or null when there's no email to send to (or email isn't set up).</summary>
    private async Task<Booking?> LoadForEmailAsync(int bookingId, CancellationToken cancellationToken)
    {
        if (!emailSender.IsConfigured) return null;
        var booking = await db.Bookings.AsNoTracking()
            .Include(x => x.Trip)
            .Include(x => x.Customer)
            .Include(x => x.TripDateSlot)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);
        return booking is null || string.IsNullOrWhiteSpace(booking.Customer.Email) ? null : booking;
    }

    private string MyBookingsUrl => $"{_options.SiteUrl.TrimEnd('/')}/my-bookings";

    private static string TravelDates(Booking b) =>
        $"{b.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture)} – {b.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}";

    private static string Money(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);
}

