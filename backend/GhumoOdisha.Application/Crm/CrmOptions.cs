namespace GhumoOdisha.Application.Crm;

/// <summary>Rules for the departure badges shown in Admin → CRM ("Crm" section of appsettings).</summary>
public class CrmOptions
{
    public const string SectionName = "Crm";

    /// <summary>"Filling fast" once at least this percentage of seats is booked.</summary>
    public int FillingFastPercent { get; set; } = 50;

    /// <summary>"Almost full" once this many seats or fewer are left.</summary>
    public int AlmostFullSeatsLeft { get; set; } = 3;

    /// <summary>"Upcoming" follow-ups are those due within this many days after today.</summary>
    public int UpcomingFollowUpDays { get; set; } = 7;
}
