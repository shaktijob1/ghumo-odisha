namespace GhumoOdisha.Application.Common;

/// <summary>
/// Trips run in India, so "today" — which decides whether a departure is still bookable — is the
/// India calendar date, not the UTC one (which lags by a day between midnight and 05:30 IST).
/// </summary>
public static class TripCalendar
{
    private static readonly TimeZoneInfo IndiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateOnly Today() => DateOf(DateTime.UtcNow);

    /// <summary>The India calendar date of a UTC timestamp.</summary>
    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IndiaTimeZone));

    /// <summary>The India wall-clock time of a UTC timestamp.</summary>
    public static DateTime IndiaTimeOf(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IndiaTimeZone);

    /// <summary>True once India time has reached <paramref name="time"/> on <paramref name="date"/>.</summary>
    public static bool HasReached(DateOnly date, TimeOnly time) => IndiaTimeOf(DateTime.UtcNow) >= date.ToDateTime(time);

    /// <summary>A departure on or after today is still bookable; anything earlier has already left.</summary>
    public static bool HasDeparted(DateOnly startDate) => startDate < Today();
}
