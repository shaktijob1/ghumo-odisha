using System.Globalization;
using System.Text.RegularExpressions;

namespace GhumoOdisha.Application.Cars;

/// <summary>Seat categories and customer-facing naming for cars. Matches the CK_Car_SeatCapacity check constraint.</summary>
public static partial class CarRules
{
    public static readonly IReadOnlyList<int> SeatCapacities = [5, 7, 9, 13, 15, 17, 20, 26];

    public const int MinExteriorPhotos = 1;
    public const int MinInteriorPhotos = 1;
    public const int MaxPhotosPerKind = 10;

    /// <summary>9 seats and under are "cars"; bigger ones are Tempo Travellers.</summary>
    public static bool IsTempoTraveller(int seats) => seats >= 13;

    /// <summary>"7 Seater Car", "13 Seater Tempo Traveller".</summary>
    public static string Category(int seats) => $"{seats} Seater {(IsTempoTraveller(seats) ? "Tempo Traveller" : "Car")}";

    /// <summary>"Toyota Innova Crysta" — brand is dropped when the model name already starts with it.</summary>
    public static string DisplayName(string brand, string modelName) =>
        modelName.StartsWith(brand, StringComparison.OrdinalIgnoreCase) ? modelName : $"{brand} {modelName}";

    /// <summary>"od 02 ab-1234" → "OD02AB1234", so the unique index catches the same plate typed differently.</summary>
    public static string NormalizeRegistration(string raw) => NonAlphanumeric().Replace(raw, "").ToUpperInvariant();

    public static bool IsValidRegistration(string normalized) => RegistrationPattern().IsMatch(normalized);

    /// <summary>"  bhubaneswar " → "Bhubaneswar", so search locations don't split on casing.</summary>
    public static string NormalizeCity(string raw)
    {
        var collapsed = Whitespace().Replace(raw.Trim(), " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
    }

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphanumeric();

    // State code + RTO + series + number ("OD02AB1234"), or Bharat series ("22BH1234AA").
    [GeneratedRegex("^([A-Z]{2}[0-9]{1,2}[A-Z]{0,3}[0-9]{1,4}|[0-9]{2}BH[0-9]{4}[A-Z]{1,2})$")]
    private static partial Regex RegistrationPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
