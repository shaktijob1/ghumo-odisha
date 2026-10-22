using GhumoOdisha.Domain.Entities;

namespace GhumoOdisha.Application.Common;

/// <summary>
/// The one place a departure's per-person price is worked out, always from stored data — never from
/// a price sent by the browser. A departure's own price overrides the trip's; an early-booking price
/// applies up to and including its end date (India calendar).
/// </summary>
public static class SlotPricing
{
    /// <summary>The departure's normal price: its own price if set, else the trip's.</summary>
    public static decimal RegularPrice(Trip trip, TripDateSlot? slot) => slot?.PricePerPerson ?? trip.AmountPerPerson;

    /// <summary>Whether the early-booking price is in force on <paramref name="on"/>.</summary>
    public static bool IsEarlyBird(TripDateSlot? slot, DateOnly on) =>
        slot is { EarlyBirdPrice: not null, EarlyBirdEndsOn: { } endsOn } && on <= endsOn;

    /// <summary>The per-person price charged for a booking made on <paramref name="on"/>.</summary>
    public static decimal PricePerPerson(Trip trip, TripDateSlot? slot, DateOnly on) =>
        IsEarlyBird(slot, on) ? slot!.EarlyBirdPrice!.Value : RegularPrice(trip, slot);

    /// <summary>
    /// The "From ₹…" price: the lowest price bookable today across the trip's open departures,
    /// or the trip's own price when it has none open.
    /// </summary>
    public static decimal FromPrice(Trip trip, IEnumerable<TripDateSlot> slots, DateOnly today)
    {
        var open = slots.Where(s => s.Status == Domain.Enums.TripDateSlotStatus.Active && s.StartDate >= today && s.AvailableSeats > 0).ToList();
        return open.Count == 0 ? trip.AmountPerPerson : open.Min(s => PricePerPerson(trip, s, today));
    }

    /// <summary>
    /// The price for an existing booking: the one in force on the day it was created. So a customer
    /// who started checkout on the last early-booking day pays the early price when they finish paying.
    /// </summary>
    public static decimal PricePerPerson(Trip trip, TripDateSlot? slot, Booking booking) =>
        PricePerPerson(trip, slot, TripCalendar.DateOf(booking.RequestedAt));
}
