namespace GhumoOdisha.Domain.Enums;

/// <summary>
/// The organizer's own switch for a whole trip, separate from <see cref="TripStatus"/> (which hides or
/// shows it). A Booked or Closed trip stays on the website but takes no online bookings; an admin can
/// still add one from Bookings → Add booking.
/// </summary>
public enum TripBookingState
{
    /// <summary>Customers can book any open date.</summary>
    Open = 0,

    /// <summary>Marked full by the admin (e.g. seats filled offline): every date reads as sold out.</summary>
    Booked = 1,

    /// <summary>Bookings closed by the admin: every date reads as "Seats filled", nothing is bookable online.</summary>
    Closed = 2
}
