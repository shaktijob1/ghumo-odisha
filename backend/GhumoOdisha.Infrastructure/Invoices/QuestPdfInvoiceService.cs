using System.Globalization;
using System.Text.RegularExpressions;
using GhumoOdisha.Application.Bookings;
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
/// Renders the booking invoice as a PDF on every download — nothing is stored, so it always shows
/// every payment recorded so far. Everything on it is re-read from the database at render time;
/// the only inputs are "which booking" and, for customers, "who's asking" (the customerId filter).
/// </summary>
public class QuestPdfInvoiceService(IGhumoOdishaDbContext db, IOptions<CompanyOptions> companyOptions) : IInvoiceService
{
    // Variation B tokens (docs/ui-reference.html).
    private const string Ink = "#0F1416";
    private const string Muted = "#6A7478";
    private const string Line = "#E7E9EA";
    private const string Accent = "#0F6F5C";
    private const string AccentSoft = "#E7F2EF";
    private const string Canvas = "#F7F8F8";
    private const string Wait = "#9A6A11";
    private const string Danger = "#C0483A";

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private static readonly TimeZoneInfo IndiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    private readonly CompanyOptions _company = companyOptions.Value;

    public Task<byte[]> GenerateInvoicePdfAsync(int customerId, int bookingId, CancellationToken cancellationToken = default) =>
        GenerateAsync(bookingId, customerId, cancellationToken);

    public Task<byte[]> GenerateAdminInvoicePdfAsync(int bookingId, CancellationToken cancellationToken = default) =>
        GenerateAsync(bookingId, customerId: null, cancellationToken);

    private async Task<byte[]> GenerateAsync(int bookingId, int? customerId, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.Trip).ThenInclude(t => t.PickupPoints)
            .Include(b => b.Trip).ThenInclude(t => t.Destinations)
            .Include(b => b.TripDateSlot)
            .Include(b => b.PickupPoint)
            .Include(b => b.Payments)
            .Include(b => b.Travellers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && (customerId == null || b.CustomerId == customerId), cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        if (booking.BookingStatus is not (BookingStatus.Confirmed or BookingStatus.Completed))
        {
            throw new ConflictException("An invoice is only available once a booking is confirmed.");
        }

        var couponCode = await db.CouponRedemptions.AsNoTracking()
            .Where(r => r.BookingId == bookingId)
            .Select(r => r.CouponCode.Code)
            .FirstOrDefaultAsync(cancellationToken);

        return BuildDocument(new InvoiceModel(booking, couponCode)).GeneratePdf();
    }

    private sealed record InvoiceModel(Booking Booking, string? CouponCode)
    {
        public TripDateSlot Slot => Booking.TripDateSlot;
        public Trip Trip => Booking.Trip;
        public decimal Subtotal => Booking.AmountPerPerson * Booking.NumberOfSeats;
        public IReadOnlyList<BookingPayment> Payments => Booking.Payments.OrderBy(p => p.PaidAt).ThenBy(p => p.BookingPaymentId).ToList();
    }

    private IDocument BuildDocument(InvoiceModel m) => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink).LineHeight(1.3f));

            // Letterhead on the first page only — a long traveller list flowing onto page 2 shouldn't repeat it.
            page.Content().Column(col =>
            {
                col.Item().Element(c => ComposeHeader(c, m));
                col.Item().PaddingHorizontal(36).PaddingTop(16).Element(c => ComposeContent(c, m));
            });
            page.Footer().Element(ComposeFooter);
        });
    });

    // ---------- Header ----------

    private void ComposeHeader(IContainer container, InvoiceModel m)
    {
        var b = m.Booking;
        container.Column(col =>
        {
            col.Item().Height(6).Background(Accent);
            col.Item().PaddingHorizontal(36).PaddingTop(18).PaddingBottom(14).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(_company.Name).FontSize(22).Bold().FontColor(Accent);
                    c.Item().Text("Travel. Explore. Repeat.").FontSize(9).FontColor(Muted);
                    c.Item().PaddingTop(8).Column(a =>
                    {
                        if (!string.IsNullOrWhiteSpace(_company.Address)) a.Item().Text(_company.Address).FontColor(Muted);
                        var contact = string.Join("  ·  ", new[] { _company.Phone, _company.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (contact.Length > 0) a.Item().Text(contact).FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(_company.Gstin)) a.Item().Text($"GSTIN: {_company.Gstin}").FontColor(Muted);
                    });
                });

                row.ConstantItem(200).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text("INVOICE").FontSize(20).Bold().LetterSpacing(0.08f);
                    c.Item().PaddingTop(6).AlignRight().Text(t =>
                    {
                        t.Span("Invoice No  ").FontColor(Muted);
                        t.Span($"INV-GO-{b.BookingId}").SemiBold();
                    });
                    c.Item().AlignRight().Text(t =>
                    {
                        t.Span("Issued  ").FontColor(Muted);
                        t.Span(FormatDate(b.ConfirmedAt ?? b.RequestedAt)).SemiBold();
                    });
                    c.Item().AlignRight().Text(t =>
                    {
                        t.Span("Generated  ").FontColor(Muted);
                        t.Span(FormatDate(DateTime.UtcNow)).SemiBold();
                    });
                    c.Item().PaddingTop(8).AlignRight().Element(e => StatusPill(e, b.PaymentStatus, b.RemainingAmount));
                });
            });
            col.Item().PaddingHorizontal(36).LineHorizontal(1).LineColor(Line);
        });
    }

    // ---------- Content ----------

    private void ComposeContent(IContainer container, InvoiceModel m)
    {
        container.Column(col =>
        {
            col.Spacing(14);
            col.Item().Element(c => ComposeParties(c, m));
            col.Item().Element(c => ComposeTripDetails(c, m));
            col.Item().Element(c => ComposeCharges(c, m));
            if (m.Booking.Travellers.Count > 0)
            {
                col.Item().Element(c => ComposeTravellers(c, m));
            }
            col.Item().Element(c => ComposePayments(c, m));
            col.Item().Element(c => ComposeBalance(c, m));
        });
    }

    private static void ComposeParties(IContainer container, InvoiceModel m)
    {
        var b = m.Booking;
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                SectionLabel(c.Item(), "Billed to");
                c.Item().Text(b.Customer.Name).FontSize(12).Bold();
                c.Item().Text($"+91 {b.Customer.PhoneNumber}").FontColor(Muted);
                if (!string.IsNullOrWhiteSpace(b.Customer.Email)) c.Item().Text(b.Customer.Email).FontColor(Muted);
            });

            row.ConstantItem(24);

            row.RelativeItem().Column(c =>
            {
                SectionLabel(c.Item(), "Booking");
                KeyValue(c, "Booking ref", $"GO-{b.BookingId}");
                KeyValue(c, "Booked on", FormatDate(b.RequestedAt));
                KeyValue(c, "Confirmed on", b.ConfirmedAt.HasValue ? FormatDateTime(b.ConfirmedAt.Value) : "-");
                KeyValue(c, "Booked via", Humanize(b.BookingSource.ToString()));
            });
        });
    }

    private static void ComposeTripDetails(IContainer container, InvoiceModel m)
    {
        var b = m.Booking;
        var nights = m.Slot.EndDate.DayNumber - m.Slot.StartDate.DayNumber;
        var duration = nights > 0 ? $"{nights + 1} Days / {nights} {(nights == 1 ? "Night" : "Nights")}" : "1 Day";

        // The booking's chosen pickup point if it has one, else the trip's first stop in route order.
        var orderedPickups = m.Trip.PickupPoints.OrderBy(p => p.DisplayOrder).ThenBy(p => p.PickupPointId).ToList();
        var pickup = b.PickupPoint ?? orderedPickups.FirstOrDefault();
        var reportingTime = pickup is null ? null : FormatTime(pickup.Time);

        var inclusions = new List<string>();
        if (m.Trip.IncludesStay) inclusions.Add("Stay");
        if (m.Trip.IncludesBreakfast) inclusions.Add("Breakfast");
        if (m.Trip.IncludesLunch) inclusions.Add("Lunch");
        if (m.Trip.IncludesDinner) inclusions.Add("Dinner");
        if (m.Trip.IncludesCoordinator) inclusions.Add("Trip coordinator");

        var destinations = m.Trip.Destinations.OrderBy(d => d.Name).Select(d => d.Name).ToList();

        container.Border(1).BorderColor(Line).Column(col =>
        {
            col.Item().Background(Canvas).PaddingVertical(10).PaddingHorizontal(14).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("TRIP DETAILS").FontSize(8).SemiBold().FontColor(Muted).LetterSpacing(0.08f);
                    c.Item().Text(m.Trip.Title).FontSize(13).Bold();
                    if (destinations.Count > 0) c.Item().Text(string.Join(" · ", destinations)).FontColor(Muted);
                });
                r.ConstantItem(110).AlignRight().AlignMiddle().Text(duration).SemiBold().FontColor(Accent);
            });

            col.Item().Padding(14).Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    KeyValue(c, "Departure", FormatLongDate(m.Slot.StartDate));
                    KeyValue(c, "Return / arrival", FormatLongDate(m.Slot.EndDate));
                    KeyValue(c, "Pickup point", pickup?.Location ?? "Will be shared by our team");
                    KeyValue(c, "Reporting time", reportingTime ?? "Will be shared by our team");
                });
                r.ConstantItem(24);
                r.RelativeItem().Column(c =>
                {
                    KeyValue(c, "Travellers", $"{b.NumberOfSeats} {(b.NumberOfSeats == 1 ? "seat" : "seats")}");
                    KeyValue(c, "Rooms allotted", RoomAllocation.ForSeats(b.NumberOfSeats).ToString(CultureInfo.InvariantCulture));
                    if (b.MaleCount.HasValue || b.FemaleCount.HasValue)
                    {
                        KeyValue(c, "Male / Female", $"{b.MaleCount ?? 0} / {b.FemaleCount ?? 0}");
                    }
                    KeyValue(c, "Inclusions", inclusions.Count > 0 ? string.Join(", ", inclusions) : "As per itinerary");
                    KeyValue(c, "Booking status", Humanize(b.BookingStatus.ToString()));
                });
            });
        });
    }

    private void ComposeCharges(IContainer container, InvoiceModel m)
    {
        var b = m.Booking;
        container.Column(col =>
        {
            SectionLabel(col.Item(), "Charges");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(5);
                    cd.RelativeColumn(1.2f);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Description");
                    HeaderCell(h.Cell(), "Qty", right: true);
                    HeaderCell(h.Cell(), "Rate", right: true);
                    HeaderCell(h.Cell(), "Amount", right: true);
                });

                BodyCell(table.Cell(), $"Trip package — {m.Trip.Title}");
                BodyCell(table.Cell(), b.NumberOfSeats.ToString(CultureInfo.InvariantCulture), right: true);
                BodyCell(table.Cell(), Money(b.AmountPerPerson), right: true);
                BodyCell(table.Cell(), Money(m.Subtotal), right: true);

                if (b.DiscountAmount > 0)
                {
                    var label = m.CouponCode is null ? "Discount" : $"Discount (coupon {m.CouponCode})";
                    BodyCell(table.Cell(), label);
                    BodyCell(table.Cell(), "");
                    BodyCell(table.Cell(), "");
                    BodyCell(table.Cell(), "− " + Money(b.DiscountAmount), right: true, color: Accent);
                }
            });
        });
    }

    private static void ComposeTravellers(IContainer container, InvoiceModel m)
    {
        container.Column(col =>
        {
            SectionLabel(col.Item(), "Travellers");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(40);
                    cd.RelativeColumn(4);
                    cd.RelativeColumn(1.6f);
                    cd.RelativeColumn(1.2f);
                    cd.RelativeColumn(2.4f);
                });

                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Seat");
                    HeaderCell(h.Cell(), "Name");
                    HeaderCell(h.Cell(), "Gender");
                    HeaderCell(h.Cell(), "Age");
                    HeaderCell(h.Cell(), "Aadhaar");
                });

                foreach (var t in m.Booking.Travellers.OrderBy(t => t.SeatNumber))
                {
                    BodyCell(table.Cell(), t.SeatNumber.ToString(CultureInfo.InvariantCulture), color: Muted);
                    BodyCell(table.Cell(), t.FullName);
                    BodyCell(table.Cell(), t.Gender?.ToString() ?? "-");
                    BodyCell(table.Cell(), t.Age?.ToString(CultureInfo.InvariantCulture) ?? "-");
                    BodyCell(table.Cell(), t.AadhaarLast4 is null ? "-" : $"XXXX XXXX {t.AadhaarLast4}", color: Muted);
                }
            });
        });
    }

    private void ComposePayments(IContainer container, InvoiceModel m)
    {
        container.Column(col =>
        {
            SectionLabel(col.Item(), "Payments received");
            if (m.Payments.Count == 0)
            {
                col.Item().Border(1).BorderColor(Line).Padding(12).Text("No payments recorded yet.").FontColor(Muted);
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(24);
                    cd.RelativeColumn(3.2f);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(3f);
                    cd.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "#");
                    HeaderCell(h.Cell(), "Date");
                    HeaderCell(h.Cell(), "Method");
                    HeaderCell(h.Cell(), "Reference");
                    HeaderCell(h.Cell(), "Amount", right: true);
                });

                var index = 1;
                foreach (var p in m.Payments)
                {
                    BodyCell(table.Cell(), index++.ToString(CultureInfo.InvariantCulture), color: Muted);
                    BodyCell(table.Cell(), FormatDateTime(p.PaidAt));
                    BodyCell(table.Cell(), MethodLabel(p.Method));
                    BodyCell(table.Cell(), string.IsNullOrWhiteSpace(p.Reference) ? (p.Notes ?? "-") : p.Reference, color: Muted);
                    BodyCell(table.Cell(), Money(p.Amount), right: true);
                }
            });
        });
    }

    private void ComposeBalance(IContainer container, InvoiceModel m)
    {
        var b = m.Booking;
        container.ShowEntire().Row(row =>
        {
            row.RelativeItem().Element(ComposeNotes);
            row.ConstantItem(16);
            row.ConstantItem(250).Column(col =>
            {
                TotalRow(col, "Total trip amount", Money(b.TotalAmount), bold: true);
                TotalRow(col, "Total paid", Money(b.AdvanceAmount), color: Accent);
                if (b.AdvanceAmount > b.TotalAmount)
                {
                    TotalRow(col, "Paid above total (non-refundable)", Money(b.AdvanceAmount - b.TotalAmount));
                }
                col.Item().PaddingTop(6).Background(b.RemainingAmount > 0 ? "#FBF4E6" : AccentSoft).Padding(12).Row(r =>
                {
                    r.RelativeItem().AlignMiddle().Text(b.RemainingAmount > 0 ? "Balance due" : "Fully paid").Bold()
                        .FontColor(b.RemainingAmount > 0 ? Wait : Accent);
                    r.AutoItem().AlignRight().Text(Money(b.RemainingAmount)).FontSize(15).Bold()
                        .FontColor(b.RemainingAmount > 0 ? Wait : Accent);
                });
            });
        });
    }

    private void ComposeNotes(IContainer container)
    {
        container.Background(Canvas).Padding(12).Column(col =>
        {
            col.Spacing(3);
            col.Item().Text("IMPORTANT").FontSize(8).SemiBold().FontColor(Muted).LetterSpacing(0.08f);
            Bullet(col, "Clear any balance before departure — cash, UPI and bank transfer are accepted.");
            Bullet(col, "Report 15 minutes before the reporting time with a photo ID for every traveller.");
            Bullet(col, "Cancellations up to 72 hours before departure, as per the Terms & Conditions.");
            var contact = string.Join(" or ", new[] { _company.Phone, _company.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (contact.Length > 0) Bullet(col, $"Questions? Reach us at {contact}.");
        });
    }

    // ---------- Footer ----------

    private void ComposeFooter(IContainer container)
    {
        container.PaddingHorizontal(36).PaddingBottom(20).Column(col =>
        {
            col.Item().LineHorizontal(1).LineColor(Line);
            col.Item().PaddingTop(8).Row(r =>
            {
                r.RelativeItem().Text($"Thank you for travelling with {_company.Name}. This is a computer-generated invoice and needs no signature.")
                    .FontSize(7.5f).FontColor(Muted);
                r.ConstantItem(60).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        });
    }

    // ---------- Building blocks ----------

    private static void StatusPill(IContainer container, PaymentStatus status, decimal remaining)
    {
        var (label, fg, bg) = status switch
        {
            PaymentStatus.Paid => ("PAID IN FULL", Accent, AccentSoft),
            PaymentStatus.AdvancePaid => ("PARTIALLY PAID", Wait, "#FBF4E6"),
            PaymentStatus.Refunded => ("REFUNDED", Danger, "#F9E9E6"),
            _ => (remaining > 0 ? "UNPAID" : "PAID IN FULL", remaining > 0 ? Danger : Accent, remaining > 0 ? "#F9E9E6" : AccentSoft)
        };

        container.Background(bg).PaddingVertical(3).PaddingHorizontal(9).Text(label).FontSize(8).Bold().FontColor(fg).LetterSpacing(0.06f);
    }

    private static void SectionLabel(IContainer container, string text) =>
        container.PaddingBottom(6).Text(text.ToUpperInvariant()).FontSize(8).SemiBold().FontColor(Muted).LetterSpacing(0.08f);

    private static void KeyValue(ColumnDescriptor col, string key, string value) =>
        col.Item().PaddingBottom(3).Row(r =>
        {
            r.ConstantItem(96).Text(key).FontColor(Muted);
            r.RelativeItem().Text(value).SemiBold();
        });

    private static void HeaderCell(IContainer cell, string text, bool right = false)
    {
        var c = cell.Background(Canvas).BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(8);
        (right ? c.AlignRight() : c).Text(text).FontSize(8.5f).SemiBold().FontColor(Muted);
    }

    private static void BodyCell(IContainer cell, string text, bool right = false, string? color = null)
    {
        var c = cell.BorderBottom(1).BorderColor(Line).PaddingVertical(7).PaddingHorizontal(8);
        (right ? c.AlignRight() : c).Text(text).FontColor(color ?? Ink);
    }

    private static void TotalRow(ColumnDescriptor col, string label, string value, bool bold = false, string? color = null) =>
        col.Item().PaddingVertical(3).PaddingHorizontal(12).Row(r =>
        {
            var l = r.RelativeItem().Text(label).FontColor(Muted);
            var v = r.AutoItem().AlignRight().Text(value).FontColor(color ?? Ink);
            if (bold) { l.SemiBold().FontColor(Ink); v.Bold(); } else { v.SemiBold(); }
        });

    private static void Bullet(ColumnDescriptor col, string text) =>
        col.Item().Row(r =>
        {
            r.ConstantItem(10).Text("•").FontColor(Accent);
            r.RelativeItem().Text(text).FontSize(8.5f).FontColor("#3A4346");
        });

    // Rs rather than ₹ — the default PDF font has no rupee glyph.
    private static string Money(decimal amount) => "Rs " + amount.ToString("N2", India);

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Razorpay => "Online (Razorpay)",
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.BankTransfer => "Bank transfer",
        _ => "Other"
    };

    private static DateTime ToIndia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IndiaTimeZone);

    private static string FormatDate(DateTime utc) => ToIndia(utc).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string FormatDateTime(DateTime utc) => ToIndia(utc).ToString("d MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    private static string FormatLongDate(DateOnly date) => date.ToString("ddd, d MMM yyyy", CultureInfo.InvariantCulture);

    // Pickup times are free text (the admin form saves "HH:mm"); show them as "6:30 AM" when they parse.
    private static string FormatTime(string time) =>
        TimeOnly.TryParse(time, CultureInfo.InvariantCulture, out var t) ? t.ToString("h:mm tt", CultureInfo.InvariantCulture) : time;

    private static string Humanize(string enumName) => Regex.Replace(enumName, "(?<!^)([A-Z])", " $1");
}
