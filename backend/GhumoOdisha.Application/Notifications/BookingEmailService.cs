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
            $"{booking.TripDateSlot.StartDate.ToString("dd MMM", CultureInfo.InvariantCulture)} – {booking.TripDateSlot.EndDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}",
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
}
