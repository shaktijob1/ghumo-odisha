using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips;

/// <summary>
/// Trip categories travel over the API as their names ("PoolParty", "Beach"), so the stored flag
/// values never leak into the Angular app and an unknown name is rejected rather than ignored.
/// </summary>
public static class TripCategories
{
    /// <summary>Every category, in the order the home page filter shows them.</summary>
    public static readonly IReadOnlyList<TripCategory> All = Enum.GetValues<TripCategory>().Where(c => c != TripCategory.None).ToList();

    public static IReadOnlyList<string> ToKeys(TripCategory categories) =>
        All.Where(c => categories.HasFlag(c)).Select(c => c.ToString()).ToList();

    public static TripCategory Parse(IEnumerable<string>? keys)
    {
        var result = TripCategory.None;
        foreach (var key in keys ?? [])
        {
            if (!TryParseOne(key, out var category))
            {
                throw new ValidationAppException([$"Unknown trip category \"{key}\"."]);
            }

            result |= category;
        }

        return result;
    }

    public static bool TryParseOne(string? key, out TripCategory category)
    {
        category = TripCategory.None;
        return !string.IsNullOrWhiteSpace(key)
            && Enum.TryParse(key.Trim(), ignoreCase: true, out category)
            && category != TripCategory.None
            && All.Contains(category);
    }
}
