using System.Globalization;
using System.Text;

namespace GhumoOdisha.Application.Trips;

/// <summary>
/// Free-text trip search and the "places covered" list. A trip matches when what the visitor typed
/// appears in its title, destinations, highlights or places covered — or nearly does, so a
/// misspelling like "jiranga" still finds "Jirang Monastery" and "mahendragri" finds "Mahendragiri".
/// </summary>
public static class TripSearch
{
    /// <summary>Places covered as entered by the admin: trimmed, blanks and repeats (any letter case) dropped.</summary>
    public static IReadOnlyList<string> CleanPlaces(IEnumerable<string>? places) =>
        (places ?? [])
            .SelectMany(p => (p ?? "").Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .DistinctBy(p => p.ToLowerInvariant())
            .ToList();

    /// <summary>The stored column value (one place per line), or null when there are none.</summary>
    public static string? JoinPlaces(IEnumerable<string>? places)
    {
        var clean = CleanPlaces(places);
        return clean.Count == 0 ? null : string.Join("\n", clean);
    }

    public static IReadOnlyList<string> ParsePlaces(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? [] : CleanPlaces(stored.Split('\n'));

    /// <summary>True when <paramref name="query"/> matches any of the texts, allowing small spelling differences.</summary>
    public static bool Matches(string query, IEnumerable<string?> texts)
    {
        var needle = Normalize(query);
        if (needle.Length == 0) return true;
        var needleWords = Words(needle);

        foreach (var text in texts)
        {
            var hay = Normalize(text);
            if (hay.Length == 0) continue;
            if (hay.Contains(needle, StringComparison.Ordinal)) return true;

            var hayWords = Words(hay);
            // Every word typed must match some word of the same text ("jirang monastery" → both words).
            if (needleWords.All(n => hayWords.Any(h => WordMatches(n, h)))) return true;
        }
        return false;
    }

    private static bool WordMatches(string typed, string word)
    {
        if (typed.Length < 3) return word.StartsWith(typed, StringComparison.Ordinal);
        if (word.StartsWith(typed, StringComparison.Ordinal)) return true;
        // "jiranga" for "jirang", "konarak" for "konark": a typed word that runs a little past the real one.
        if (word.Length >= 4 && typed.StartsWith(word, StringComparison.Ordinal) && typed.Length - word.Length <= 2) return true;
        if (typed.Length < 4) return false;
        // Typos: one edit allowed for short words, two for long ones (compared at the typed length too,
        // so a partly typed long name still matches).
        var allowed = typed.Length >= 8 ? 2 : 1;
        return Distance(typed, word) <= allowed
            || (word.Length > typed.Length && Distance(typed, word[..typed.Length]) <= allowed);
    }

    /// <summary>Lower case, accents removed, punctuation turned into spaces.</summary>
    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string[] Words(string normalized) => normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Levenshtein edit distance.</summary>
    private static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }
}
