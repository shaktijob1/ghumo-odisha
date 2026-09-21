namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Collapses the various ways an Indian mobile number can be typed (9876543210,
/// +919876543210, 919876543210) to the single bare 10-digit form already used
/// everywhere else in this codebase (matches the "^[6-9][0-9]{9}$" validators),
/// so formatting differences never create duplicate customer identities.
/// </summary>
public static class PhoneNumberNormalizer
{
    public static string Normalize(string input)
    {
        var digitsOnly = new string(input.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length == 12 && digitsOnly.StartsWith("91"))
        {
            digitsOnly = digitsOnly[2..];
        }

        return digitsOnly;
    }
}
