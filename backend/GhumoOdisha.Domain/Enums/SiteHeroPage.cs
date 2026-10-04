namespace GhumoOdisha.Domain.Enums;

/// <summary>Which admin-uploaded site photo a <see cref="Entities.SiteHeroPhoto"/> is: a page banner, or the office photo.</summary>
public enum SiteHeroPage
{
    Home = 0,
    /// <summary>The old Trips list page banner (that page now redirects home; kept so stored values still read).</summary>
    Trips = 1,

    /// <summary>Photo of the office, shown in the site footer next to its address.</summary>
    Office = 2
}
