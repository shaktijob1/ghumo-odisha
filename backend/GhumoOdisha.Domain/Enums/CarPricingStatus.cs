namespace GhumoOdisha.Domain.Enums;

/// <summary>
/// Pricing rows are never edited once reviewed — every change is a new row, so history is kept.
/// Approving a row supersedes the car's previously approved one.
/// </summary>
public enum CarPricingStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Superseded = 3,
    /// <summary>A pending proposal replaced by a newer one before the admin reviewed it.</summary>
    Withdrawn = 4
}
