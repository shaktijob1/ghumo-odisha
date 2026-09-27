using System.Net;
using System.Text;

namespace GhumoOdisha.Application.Notifications;

/// <summary>
/// Transactional email bodies. Styles are inline (email clients strip &lt;style&gt;) and follow the
/// site's Variation B tokens: white card, #E7E9EA hairlines, #0F6F5C accent, 14px radius.
/// Every dynamic value goes through <see cref="E"/> — customer names and trip titles are user input.
/// </summary>
public static class EmailTemplates
{
    private const string Ink = "#0F1416";
    private const string Muted = "#6A7478";
    private const string Line = "#E7E9EA";
    private const string Accent = "#0F6F5C";

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    public static (string Html, string Text) Otp(string code, int expiryMinutes, string purpose)
    {
        var body = $"""
            <p style="margin:0 0 16px">Use this code to {E(purpose)}:</p>
            <div style="font-size:30px;font-weight:600;letter-spacing:8px;color:{Ink};background:#F7F8F8;border:1px solid {Line};border-radius:12px;padding:14px 0;text-align:center">{E(code)}</div>
            <p style="margin:16px 0 0;color:{Muted};font-size:13px">It expires in {expiryMinutes} minutes. If you didn't ask for it, you can ignore this email — nobody can sign in without the code.</p>
            """;
        var text = $"Your Ghumo Odisha code is {code}. Use it to {purpose}. It expires in {expiryMinutes} minutes. " +
                   "If you didn't ask for it, ignore this email.";
        return (Layout("Your verification code", body), text);
    }

    public record BookingConfirmedModel(
        string CustomerName,
        string BookingReference,
        string TripTitle,
        string TravelDates,
        int Seats,
        string TotalAmount,
        string AmountPaid,
        string Balance,
        string PickupPoint,
        string ReportingTime,
        string MyBookingsUrl,
        bool InvoiceAttached);

    public static (string Html, string Text) BookingConfirmed(BookingConfirmedModel m)
    {
        string Row(string label, string value) =>
            $"""<tr><td style="padding:9px 0;color:{Muted};border-bottom:1px solid {Line}">{E(label)}</td><td style="padding:9px 0;text-align:right;font-weight:600;border-bottom:1px solid {Line}">{E(value)}</td></tr>""";

        var rows = new StringBuilder()
            .Append(Row("Booking ID", m.BookingReference))
            .Append(Row("Trip", m.TripTitle))
            .Append(Row("Dates", m.TravelDates))
            .Append(Row("Seats", m.Seats.ToString()))
            .Append(Row("Pickup point", m.PickupPoint))
            .Append(Row("Reporting time", m.ReportingTime))
            .Append(Row("Total", "₹" + m.TotalAmount))
            .Append(Row("Paid", "₹" + m.AmountPaid))
            .Append(Row("Balance", "₹" + m.Balance))
            .ToString();

        var invoiceNote = m.InvoiceAttached ? "Your invoice is attached. " : "";
        var body = $"""
            <p style="margin:0 0 6px">Hi {E(m.CustomerName)},</p>
            <p style="margin:0 0 18px">Your booking is <b style="color:{Accent}">confirmed</b> — your seats are reserved. {invoiceNote}See you on the trip!</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-size:14px;border-collapse:collapse">{rows}</table>
            <div style="text-align:center;margin:24px 0 4px">
              <a href="{E(m.MyBookingsUrl)}" style="display:inline-block;background:{Accent};color:#FFFFFF;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:999px">View my bookings</a>
            </div>
            """;

        var text = $"Hi {m.CustomerName}, your booking {m.BookingReference} is confirmed.\n" +
                   $"Trip: {m.TripTitle}\nDates: {m.TravelDates}\nSeats: {m.Seats}\n" +
                   $"Pickup: {m.PickupPoint} ({m.ReportingTime})\n" +
                   $"Total: Rs {m.TotalAmount} | Paid: Rs {m.AmountPaid} | Balance: Rs {m.Balance}\n" +
                   $"{invoiceNote}View your bookings: {m.MyBookingsUrl}";
        return (Layout($"Booking {m.BookingReference} confirmed", body), text);
    }

    public record RefundCompletedModel(
        string CustomerName,
        string BookingReference,
        string TripTitle,
        string Amount,
        string MethodLabel,
        string? Reference,
        string MyBookingsUrl);

    public static (string Html, string Text) RefundCompleted(RefundCompletedModel m)
    {
        var via = string.IsNullOrWhiteSpace(m.MethodLabel) ? "" : $" by {m.MethodLabel}";
        var reference = string.IsNullOrWhiteSpace(m.Reference) ? "" : $"""<p style="margin:0 0 16px;color:{Muted};font-size:13px">Reference: {E(m.Reference)}</p>""";
        var body = $"""
            <p style="margin:0 0 6px">Hi {E(m.CustomerName)},</p>
            <p style="margin:0 0 16px">The refund of <b>{E(m.Amount)}</b> for your cancelled booking <b>{E(m.BookingReference)}</b> ({E(m.TripTitle)}) has been processed{E(via)}.
            Depending on your bank it can take a few working days to show in your account.</p>
            {reference}
            <div style="text-align:center;margin:20px 0 4px">
              <a href="{E(m.MyBookingsUrl)}" style="display:inline-block;background:{Accent};color:#FFFFFF;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:999px">View my bookings</a>
            </div>
            """;
        var text = $"Hi {m.CustomerName}, the refund of {m.Amount} for booking {m.BookingReference} ({m.TripTitle}) has been processed{via}. " +
                   $"It can take a few working days to show in your account." +
                   (string.IsNullOrWhiteSpace(m.Reference) ? "" : $" Reference: {m.Reference}.") +
                   $" {m.MyBookingsUrl}";
        return (Layout("Your refund has been processed", body), text);
    }

    private static string Layout(string heading, string bodyHtml) => $"""
        <!doctype html>
        <html><body style="margin:0;padding:0;background:#F7F8F8;font-family:Inter,Segoe UI,Helvetica,Arial,sans-serif;color:{Ink}">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F7F8F8;padding:28px 12px">
            <tr><td align="center">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#FFFFFF;border:1px solid {Line};border-radius:14px">
                <tr><td style="padding:22px 26px;border-bottom:1px solid {Line};font-size:17px;font-weight:600">Ghumo <span style="color:{Accent}">Odisha</span></td></tr>
                <tr><td style="padding:24px 26px;font-size:15px;line-height:1.55">
                  <h1 style="margin:0 0 16px;font-size:20px;font-weight:600">{E(heading)}</h1>
                  {bodyHtml}
                </td></tr>
              </table>
              <p style="margin:14px 0 0;font-size:12px;color:{Muted}">Ghumo Odisha · This is an automated message.</p>
            </td></tr>
          </table>
        </body></html>
        """;
}
