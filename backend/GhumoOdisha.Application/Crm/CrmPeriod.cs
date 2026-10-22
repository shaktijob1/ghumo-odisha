using GhumoOdisha.Application.Exceptions;

namespace GhumoOdisha.Application.Crm;

/// <summary>A calendar month (India time) as a UTC range — so "October" means October in India, for any year.</summary>
public readonly record struct CrmPeriod(DateTime FromUtc, DateTime ToUtc)
{
    private static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    /// <summary>Null when no month is selected. A month without a year is rejected.</summary>
    public static CrmPeriod? FromMonth(int? year, int? month)
    {
        if (year is null && month is null)
        {
            return null;
        }

        if (year is null or < 2000 or > 2100)
        {
            throw new ValidationAppException(["Choose a year between 2000 and 2100."]);
        }

        if (month is null)
        {
            return Between(new DateOnly(year.Value, 1, 1), new DateOnly(year.Value, 12, 31));
        }

        if (month is < 1 or > 12)
        {
            throw new ValidationAppException(["Choose a month between 1 and 12."]);
        }

        var first = new DateOnly(year.Value, month.Value, 1);
        return Between(first, first.AddMonths(1).AddDays(-1));
    }

    /// <summary>From the start of <paramref name="from"/> to the end of <paramref name="to"/>, India time.</summary>
    public static CrmPeriod Between(DateOnly from, DateOnly to) =>
        new(StartOfDayUtc(from), StartOfDayUtc(to.AddDays(1)));

    public static DateOnly FirstDay(int year, int month) => new(year, month, 1);

    private static DateTime StartOfDayUtc(DateOnly indiaDate) =>
        TimeZoneInfo.ConvertTimeToUtc(indiaDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), India);
}
