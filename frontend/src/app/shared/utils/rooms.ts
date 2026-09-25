/**
 * Rooms allotted for a number of seats: one per two seats, rounded down (5 → 2, 9 → 4), minimum one.
 * Display-only mirror of GhumoOdisha.Application.Bookings.RoomAllocation — the API is the source of truth.
 */
export function roomsForSeats(seats: number): number {
  return seats <= 0 ? 0 : Math.max(1, Math.floor(seats / 2));
}
