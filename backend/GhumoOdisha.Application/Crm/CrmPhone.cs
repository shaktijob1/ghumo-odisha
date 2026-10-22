using GhumoOdisha.Application.Auth;

namespace GhumoOdisha.Application.Crm;

/// <summary>Indian mobile numbers for the CRM: stored as 10 digits, like every customer phone.</summary>
public static class CrmPhone
{
    /// <summary>"+91 98765-43210", "09876543210", "919876543210" → "9876543210"; null when it isn't a valid Indian mobile.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var digits = PhoneNumberNormalizer.Normalize(input);
        if (digits.Length == 11 && digits[0] == '0')
        {
            digits = digits[1..];
        }

        return IsValid(digits) ? digits : null;
    }

    public static bool IsValid(string digits) =>
        digits.Length == 10 && digits[0] is >= '6' and <= '9' && digits.All(char.IsDigit);

    /// <summary>The number as wa.me wants it: country code + number, digits only.</summary>
    public static string? ForWhatsApp(string? storedPhone) =>
        Normalize(storedPhone) is { } digits ? "91" + digits : null;

    public static string AppUrl(string waPhone, string message) =>
        $"https://wa.me/{waPhone}?text={Uri.EscapeDataString(message)}";

    public static string WebUrl(string waPhone, string message) =>
        $"https://web.whatsapp.com/send?phone={waPhone}&text={Uri.EscapeDataString(message)}";
}
