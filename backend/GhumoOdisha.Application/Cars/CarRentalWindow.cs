using GhumoOdisha.Application.Exceptions;

namespace GhumoOdisha.Application.Cars;

/// <summary>A validated rental window in UTC, plus the India-calendar nights it spans.</summary>
public record CarRentalWindow(DateTime StartUtc, DateTime EndUtc, int DurationHours, int Nights)
{
    /// <summary>
    /// Turns the customer's India date/time + hours into a UTC window, rejecting anything outside the
    /// business rules (too soon, too far ahead, too short/long) with a message the customer can act on.
    /// </summary>
    public static CarRentalWindow Resolve(DateOnly date, TimeOnly time, int durationHours, CarRentalOptions options, DateTime nowUtc)
    {
        var errors = new List<string>();
        if (durationHours < options.MinDurationHours || durationHours > options.MaxDurationHours)
        {
            errors.Add($"Choose a duration between {options.MinDurationHours} hours and {options.MaxDurationHours / 24} days.");
        }

        var startUtc = CarRentalCalendar.IndiaToUtc(date, time);
        if (startUtc < nowUtc.AddMinutes(options.MinimumLeadMinutes))
        {
            errors.Add(options.MinimumLeadMinutes >= 60
                ? $"Pickup must be at least {options.MinimumLeadMinutes / 60} hour{(options.MinimumLeadMinutes >= 120 ? "s" : "")} from now."
                : $"Pickup must be at least {options.MinimumLeadMinutes} minutes from now.");
        }
        if (startUtc > nowUtc.AddDays(options.MaxAdvanceBookingDays))
        {
            errors.Add($"Cars can be booked up to {options.MaxAdvanceBookingDays} days ahead.");
        }
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var endUtc = startUtc.AddHours(durationHours);
        return new CarRentalWindow(startUtc, endUtc, durationHours, CarRentalCalendar.NightsSpanned(startUtc, endUtc));
    }
}
