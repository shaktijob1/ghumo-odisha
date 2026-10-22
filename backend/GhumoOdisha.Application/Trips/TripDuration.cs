using GhumoOdisha.Domain.Entities;

namespace GhumoOdisha.Application.Trips;

/// <summary>
/// The one place a trip's "4 Days / 3 Nights" is worked out: the admin's own figures when set
/// (a night departure isn't a day of the trip), else counted from the departure's dates.
/// </summary>
public static class TripDuration
{
    public static (int Days, int Nights) Of(Trip trip, DateOnly start, DateOnly end)
    {
        if (trip.DurationDays is { } days && trip.DurationNights is { } nights)
        {
            return (days, nights);
        }

        var counted = end.DayNumber - start.DayNumber + 1;
        return (counted, Math.Max(0, counted - 1));
    }

    /// <summary>"4 Days / 3 Nights" — same wording on the cards, trip page and SEO pages.</summary>
    public static string Label(Trip trip, DateOnly start, DateOnly end)
    {
        var (days, nights) = Of(trip, start, end);
        return $"{days} {(days == 1 ? "Day" : "Days")} / {nights} {(nights == 1 ? "Night" : "Nights")}";
    }
}
