namespace GhumoOdisha.Domain.Enums;

/// <summary>A driver only receives bookings once Approved. Only an admin moves a driver out of Pending.</summary>
public enum DriverStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Suspended = 3
}
