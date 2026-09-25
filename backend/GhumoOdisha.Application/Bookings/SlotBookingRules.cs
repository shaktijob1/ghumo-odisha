using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Bookings;

/// <summary>
/// The one set of checks every path that takes a booking onto a date slot runs first — customer
/// request, payment order creation, manual admin booking and confirm — so a tampered request can
/// never land a booking on a hidden, departed or full departure. The guarded UPDATE in
/// <see cref="BookingService"/> remains the final word on seats; this just rejects early and clearly.
/// </summary>
public static class SlotBookingRules
{
    public static void EnsureBookable(TripDateSlot slot, int seats)
    {
        if (slot.Status != TripDateSlotStatus.Active)
        {
            throw new DepartureClosedException("This date is no longer available.");
        }

        if (TripCalendar.HasDeparted(slot.StartDate))
        {
            throw new DepartureClosedException("This departure date has already passed.");
        }

        if (slot.AvailableSeats <= 0)
        {
            throw new ConflictException("This date is fully booked.");
        }

        if (seats > slot.AvailableSeats)
        {
            throw new ConflictException($"Only {slot.AvailableSeats} seat(s) are available on this date.");
        }
    }
}
