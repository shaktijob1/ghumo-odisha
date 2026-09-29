namespace GhumoOdisha.Domain.Enums;

/// <summary>A car is searchable only while Approved (and its driver and active pricing are approved too).</summary>
public enum CarStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Taken off the site (by the admin, or by the driver for their own car). Can be resubmitted.</summary>
    Inactive = 3,
    /// <summary>Blocked by the admin; only the admin can lift it.</summary>
    Suspended = 4
}
