namespace GhumoOdisha.Application.Common;

/// <summary>Site-wide switches for parts of the customer site that are still being built.</summary>
public class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>
    /// While true, customers see Trips only: the home page hides the Cars and Holidays search tabs,
    /// and the customer Cars pages send visitors to the home page. The driver area and the admin
    /// Cars screens keep working, so drivers and cars can be onboarded before launch.
    /// </summary>
    public bool HideCarsAndHolidays { get; set; } = true;
}
