namespace GhumoOdisha.Application.Cars;

/// <summary>
/// Car pickups are chosen in India time ("2 Oct 2026, 10:00 AM") and stored in UTC. Nights are counted
/// on the India calendar too — a night halt is a night the trip spans in Odisha, whatever the server's zone.
/// </summary>
public static class CarRentalCalendar
{
    private static readonly TimeZoneInfo IndiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    public static DateTime IndiaToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified), IndiaTimeZone);

    public static DateTime UtcToIndia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IndiaTimeZone);

    /// <summary>
    /// India midnights strictly inside (start, end): 10:00 → 22:00 the same day is 0 nights, 10:00 → 10:00 the
    /// next day is 1, and a trip ending exactly at midnight doesn't count that midnight.
    /// </summary>
    public static int NightsSpanned(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
        {
            return 0;
        }

        var start = UtcToIndia(startUtc);
        var end = UtcToIndia(endUtc);
        var nights = end.Date.DayNumber() - start.Date.DayNumber();
        if (end.TimeOfDay == TimeSpan.Zero)
        {
            nights--;
        }
        return Math.Max(nights, 0);
    }

    private static int DayNumber(this DateTime date) => DateOnly.FromDateTime(date).DayNumber;
}
