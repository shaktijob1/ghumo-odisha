namespace GhumoOdisha.Application.Common;

/// <summary>
/// Trips run in India, so "today" — which decides whether a departure is still bookable — is the
/// India calendar date, not the UTC one (which lags by a day between midnight and 05:30 IST).
/// </summary>
public static class TripCalendar
{
    private static readonly TimeZoneInfo IndiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IndiaTimeZone));

    /// <summary>A departure on or after today is still bookable; anything earlier has already left.</summary>
    public static bool HasDeparted(DateOnly startDate) => startDate < Today();
}
