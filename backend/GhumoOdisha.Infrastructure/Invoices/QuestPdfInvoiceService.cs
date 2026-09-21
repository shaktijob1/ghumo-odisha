using System.Text.RegularExpressions;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Invoices;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GhumoOdisha.Infrastructure.Invoices;

/// <summary>
/// Renders a one-page PDF invoice for a confirmed booking. Everything on it is re-read from the
/// booking/trip/customer rows at render time — never trusts anything the caller sends beyond
/// "which booking" and "who's asking" (enforced by the customerId filter below).
/// </summary>
public class QuestPdfInvoiceService(IGhumoOdishaDbContext db, IOptions<CompanyOptions> companyOptions) : IInvoiceService
{
    private readonly CompanyOptions _company = companyOptions.Value;

    public async Task<byte[]> GenerateInvoicePdfAsync(int customerId, int bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Trip)
            .Include(b => b.TripDateSlot)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        if (booking.BookingStatus is not (BookingStatus.Confirmed or BookingStatus.Completed))
        {
            throw new ConflictException("An invoice is only available once a booking is confirmed.");
        }

        return BuildDocument(booking).GeneratePdf();
    }

    private QuestPDF.Infrastructure.IDocument BuildDocument(Booking booking)
    {
        var nights = booking.TripDateSlot.EndDate.DayNumber - booking.TripDateSlot.StartDate.DayNumber;
        var durationLabel = nights > 0 ? $"{nights + 1} Days / {nights} Nights" : "1 Day";
        var paymentMethod = string.IsNullOrWhiteSpace(booking.RazorpayPaymentId) ? "Offline / Manual" : "Online (Razorpay)";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(_company.Name).FontSize(20).Bold().FontColor("#0F6F5C");
                            c.Item().Text("Trip Booking Invoice").FontSize(11).FontColor("#6A7478");
                        });

                        row.ConstantItem(220).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Invoice No: INV-GO-{booking.BookingId}").Bold();
                            c.Item().AlignRight().Text($"Invoice Date: {(booking.ConfirmedAt ?? booking.RequestedAt):d MMM yyyy}");
                            c.Item().AlignRight().Text($"Booking Ref: GO-{booking.BookingId}");
                        });
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#E7E9EA");
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Spacing(16);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("From").FontSize(9).FontColor("#6A7478");
                            c.Item().Text(_company.Name).Bold();
                            if (!string.IsNullOrWhiteSpace(_company.Address)) c.Item().Text(_company.Address);
                            if (!string.IsNullOrWhiteSpace(_company.Phone)) c.Item().Text($"Phone: {_company.Phone}");
                            if (!string.IsNullOrWhiteSpace(_company.Email)) c.Item().Text($"Email: {_company.Email}");
                            if (!string.IsNullOrWhiteSpace(_company.Gstin)) c.Item().Text($"GSTIN: {_company.Gstin}");
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Bill To").FontSize(9).FontColor("#6A7478");
                            c.Item().Text(booking.Customer.Name).Bold();
                            c.Item().Text($"Phone: {booking.Customer.PhoneNumber}");
                            if (!string.IsNullOrWhiteSpace(booking.Customer.Email)) c.Item().Text($"Email: {booking.Customer.Email}");
                        });
                    });

                    col.Item().Text("Trip Details").FontSize(12).Bold().FontColor("#0F1416");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(3);
                        });

                        AddRow(table, "Trip", booking.Trip.Title);
                        AddRow(table, "Travel Dates", $"{booking.TripDateSlot.StartDate:d MMM yyyy} to {booking.TripDateSlot.EndDate:d MMM yyyy}");
                        AddRow(table, "Duration", durationLabel);
                        AddRow(table, "Number of Seats", booking.NumberOfSeats.ToString());
                        AddRow(table, "Price per Seat", $"Rs {booking.AmountPerPerson:N2}");
                        AddRow(table, "Booking Status", Humanize(booking.BookingStatus.ToString()));
                    });

                    col.Item().Text("Payment Summary").FontSize(12).Bold().FontColor("#0F1416");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(3);
                        });

                        AddRow(table, "Total Trip Amount", $"Rs {booking.TotalAmount:N2}");
                        AddRow(table, "Amount Paid", $"Rs {booking.AdvanceAmount:N2}");
                        AddRow(table, "Balance Due", $"Rs {booking.RemainingAmount:N2}");
                        AddRow(table, "Payment Status", Humanize(booking.PaymentStatus.ToString()));
                        AddRow(table, "Payment Method", paymentMethod);
                        if (!string.IsNullOrWhiteSpace(booking.RazorpayPaymentId))
                        {
                            AddRow(table, "Payment Reference", booking.RazorpayPaymentId);
                        }
                        AddRow(table, "Confirmed On", booking.ConfirmedAt.HasValue ? booking.ConfirmedAt.Value.ToString("d MMM yyyy, h:mm tt") : "-");
                    });

                    col.Item().PaddingTop(6).Background("#F7F8F8").Padding(12).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text("Amount Paid").Bold();
                        row.ConstantItem(160).AlignRight().Text($"Rs {booking.AdvanceAmount:N2}").FontSize(14).Bold().FontColor("#0F6F5C");
                    });
                });

                page.Footer().PaddingTop(10).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#E7E9EA");
                    col.Item().PaddingTop(8).AlignCenter().Text("This is a system-generated invoice and does not require a signature.").FontSize(8).FontColor("#6A7478");
                    col.Item().AlignCenter().Text($"Thank you for booking with {_company.Name}.").FontSize(8).FontColor("#6A7478");
                });
            });
        });
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(4).Text(label).FontColor("#6A7478");
        table.Cell().PaddingVertical(4).Text(value).Bold();
    }

    private static string Humanize(string enumName) => Regex.Replace(enumName, "(?<!^)([A-Z])", " $1");
}
