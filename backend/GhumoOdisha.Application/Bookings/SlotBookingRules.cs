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
    /// <summary>
    /// Online booking closes this many days before departure: the trip day and the 2 days before it show
    /// as "Seats filled" to customers, who pick a later departure. Only an admin can still add a booking then.
    /// </summary>
    public const int OnlineCutoffDays = 2;

    public const string OnlineClosedMessage = "Seats are filled for this date. Please choose one of the next departures.";

    /// <summary>Too close to departure for customers to book online (Indian calendar day).</summary>
    public static bool IsClosedOnline(DateOnly startDate) => startDate <= TripCalendar.Today().AddDays(OnlineCutoffDays);

    /// <param name="online">True for anything the customer does on the website; false for the admin's own bookings.</param>
    public static void EnsureBookable(TripDateSlot slot, int seats, bool online = true)
    {
        if (slot.Status != TripDateSlotStatus.Active)
        {
            throw new DepartureClosedException("This date is no longer available.");
        }

        if (TripCalendar.HasDeparted(slot.StartDate))
        {
            throw new DepartureClosedException("This departure date has already passed.");
        }

        if (online && IsClosedOnline(slot.StartDate))
        {
            throw new DepartureClosedException(OnlineClosedMessage);
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
