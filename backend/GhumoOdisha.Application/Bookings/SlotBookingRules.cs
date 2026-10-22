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
    /// Online booking stays open until this time (India) on the morning of departure; after that the
    /// date shows as "Seats filled". Only an admin can still add a booking then.
    /// </summary>
    public static readonly TimeOnly OnlineCloseTime = new(6, 0);

    /// <summary>"6 AM" — for customer-facing text.</summary>
    public const string OnlineCloseTimeLabel = "6 AM";

    public const string OnlineClosedMessage = "Seats are filled for this date. Please choose one of the next departures.";

    public const string TripBookedMessage = "This trip is fully booked. Please message us on WhatsApp for the next departure.";

    public const string TripClosedMessage = "Bookings for this trip are closed right now. Please message us on WhatsApp.";

    /// <summary>Too close to departure for customers to book online: 6 AM (India) on the trip day has passed.</summary>
    public static bool IsClosedOnline(DateOnly startDate) => TripCalendar.HasReached(startDate, OnlineCloseTime);

    /// <summary>Not bookable online: the admin closed the trip, or the date is past its online cutoff.</summary>
    public static bool IsClosedOnline(Trip trip, DateOnly startDate) =>
        trip.BookingState == TripBookingState.Closed || IsClosedOnline(startDate);

    /// <summary>The admin marked the whole trip as booked: every date reads as sold out.</summary>
    public static bool IsMarkedBooked(Trip trip) => trip.BookingState == TripBookingState.Booked;

    /// <summary>Anything the customer does on the website: the trip's Booked / Closed state and the online cutoff apply.</summary>
    public static void EnsureBookable(Trip trip, TripDateSlot slot, int seats) => Ensure(trip, slot, seats, online: true);

    /// <summary>The admin's own bookings (phone / walk-in): allowed on a Booked or Closed trip and after the online cutoff.</summary>
    public static void EnsureBookableByAdmin(TripDateSlot slot, int seats) => Ensure(null, slot, seats, online: false);

    private static void Ensure(Trip? trip, TripDateSlot slot, int seats, bool online)
    {
        if (slot.Status != TripDateSlotStatus.Active)
        {
            throw new DepartureClosedException("This date is no longer available.");
        }

        if (TripCalendar.HasDeparted(slot.StartDate))
        {
            throw new DepartureClosedException("This departure date has already passed.");
        }

        if (online)
        {
            if (trip!.BookingState == TripBookingState.Booked)
            {
                throw new DepartureClosedException(TripBookedMessage);
            }

            if (trip.BookingState == TripBookingState.Closed)
            {
                throw new DepartureClosedException(TripClosedMessage);
            }

            if (IsClosedOnline(slot.StartDate))
            {
                throw new DepartureClosedException(OnlineClosedMessage);
            }
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
