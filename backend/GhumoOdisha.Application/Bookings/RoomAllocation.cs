namespace GhumoOdisha.Application.Bookings;

/// <summary>
/// Rooms allotted to a booking: one room per two seats, rounded down (5 seats → 2 rooms,
/// 9 → 4), but never fewer than one room for a booking that has seats.
/// </summary>
public static class RoomAllocation
{
    public static int ForSeats(int seats) => seats <= 0 ? 0 : Math.Max(1, seats / 2);
}
