using System.Globalization;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Notifications;

public interface IBookingEmailService
{
    /// <summary>Best-effort: never throws — a failed email must not fail a booking confirmation.</summary>
    Task SendBookingConfirmedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer,
        BookingConfirmedWhatsAppMessage details, CancellationToken cancellationToken = default);
}

public class BookingEmailService(
    IEmailSender emailSender,
    IInvoiceService invoiceService,
    IOptions<EmailOptions> emailOptions,
    ILogger<BookingEmailService> logger) : IBookingEmailService
{
    private readonly EmailOptions _options = emailOptions.Value;

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
                attachments.Add(new EmailAttachment($"Invoice-GO-{booking.BookingId}.pdf", "application/pdf", pdf));
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
