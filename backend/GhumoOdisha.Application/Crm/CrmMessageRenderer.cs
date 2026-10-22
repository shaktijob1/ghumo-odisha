using System.Globalization;
using System.Text.RegularExpressions;

namespace GhumoOdisha.Application.Crm;

/// <summary>Values for one customer (and optionally one trip) that a WhatsApp template can use. Null = not known.</summary>
public record CrmMessageValues(
    string? Name,
    string? Trip,
    DateOnly? Date,
    DateOnly? ReturnDate,
    decimal? Price,
    decimal? BookingAmount,
    int? AvailableSeats,
    int? TotalSeats,
    int? Travellers,
    string? Pickup,
    string? Vehicle,
    string? Stay,
    decimal? Balance,
    string? BookingId);

/// <summary>
/// Fills {name}, {trip}, {date} … in a WhatsApp template. Only the placeholders below exist; a value
/// that isn't known for this customer/trip is left blank and reported, so the admin sees it in the
/// preview before opening WhatsApp. Money is shown without the ₹ sign (templates write "₹{price}").
/// </summary>
public static partial class CrmMessageRenderer
{
    public static readonly IReadOnlyList<string> Placeholders =
    [
        "name", "trip", "date", "return_date", "price", "booking_amount", "available_seats", "total_seats",
        "travellers", "pickup", "vehicle", "stay", "balance", "booking_id"
    ];

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    [GeneratedRegex(@"\{([a-zA-Z_]+)\}")]
    private static partial Regex PlaceholderPattern();

    /// <summary>Placeholders in <paramref name="body"/> that aren't supported (typos like {nmae}).</summary>
    public static IReadOnlyList<string> UnknownPlaceholders(string body) =>
        PlaceholderPattern().Matches(body)
            .Select(m => m.Groups[1].Value)
            .Where(p => !Placeholders.Contains(p.ToLowerInvariant()))
            .Distinct()
            .ToList();

    public static (string Message, IReadOnlyList<string> Missing) Render(string body, CrmMessageValues values)
    {
        var missing = new List<string>();
        var lines = new List<string>();
        foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
        {
            var filled = Fill(line, values, missing);
            // "🏨 {stay}" with no stay would leave a lone emoji — drop a line that a missing value left
            // with no words or numbers at all. The missing value is still reported to the admin.
            if (PlaceholderPattern().IsMatch(line) && !filled.Any(char.IsLetterOrDigit))
            {
                continue;
            }

            lines.Add(filled);
        }

        return (string.Join("\n", lines), missing);
    }

    private static string Fill(string text, CrmMessageValues values, List<string> missing) =>
        PlaceholderPattern().Replace(text, match =>
        {
            var key = match.Groups[1].Value.ToLowerInvariant();
            if (!Placeholders.Contains(key))
            {
                return match.Value;
            }

            var value = ValueOf(key, values);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!missing.Contains(key))
                {
                    missing.Add(key);
                }

                return "";
            }

            return value;
        });

    private static string? ValueOf(string key, CrmMessageValues v) => key switch
    {
        "name" => v.Name,
        "trip" => v.Trip,
        "date" => FormatDate(v.Date),
        "return_date" => FormatDate(v.ReturnDate),
        "price" => Money(v.Price),
        "booking_amount" => Money(v.BookingAmount),
        "available_seats" => v.AvailableSeats?.ToString(CultureInfo.InvariantCulture),
        "total_seats" => v.TotalSeats?.ToString(CultureInfo.InvariantCulture),
        "travellers" => v.Travellers?.ToString(CultureInfo.InvariantCulture),
        "pickup" => v.Pickup,
        "vehicle" => v.Vehicle,
        "stay" => v.Stay,
        "balance" => Money(v.Balance),
        "booking_id" => v.BookingId,
        _ => null
    };

    /// <summary>"14-Nov-2026".</summary>
    public static string? FormatDate(DateOnly? date) => date?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>"5,499" or "1,23,456.50" (Indian grouping, paise only when there are any).</summary>
    public static string? Money(decimal? amount) =>
        amount is null ? null : amount.Value.ToString(decimal.Truncate(amount.Value) == amount.Value ? "#,##0" : "#,##0.00", India);
}
