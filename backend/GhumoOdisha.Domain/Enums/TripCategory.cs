namespace GhumoOdisha.Domain.Enums;

/// <summary>
/// The kinds of trip the home page filter offers ("Pool Party", "Trekking"…). A trip can be several
/// at once, so it's stored as flags in one int column.
/// </summary>
[Flags]
public enum TripCategory
{
    None = 0,
    PoolParty = 1,
    Adventure = 2,
    Trekking = 4,
    Beach = 8,
    Camping = 16,
    Waterfalls = 32,
    Heritage = 64,
    Hills = 128
}
