using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Crm;

/// <summary>
/// Works out a departure's badge from its seats and dates. "Confirmed" is only ever shown once the
/// departure's own minimum number of booked seats is reached — never just because seats were sold.
/// </summary>
public static class CrmDepartureRules
{
    public static bool MinimumReached(TripDateSlot slot) =>
        slot.MinimumBookedSeats is { } minimum && slot.TotalSeats - slot.AvailableSeats >= minimum;

    public static CrmDepartureStatus StatusOf(TripDateSlot slot, DateOnly today, CrmOptions options)
    {
        var booked = slot.TotalSeats - slot.AvailableSeats;

        if (slot.EndDate < today)
        {
            return CrmDepartureStatus.Completed;
        }

        if (slot.Status != TripDateSlotStatus.Active)
        {
            return CrmDepartureStatus.Draft;
        }

        if (slot.AvailableSeats <= 0)
        {
            return CrmDepartureStatus.SoldOut;
        }

        if (slot.AvailableSeats <= options.AlmostFullSeatsLeft && booked > 0)
        {
            return CrmDepartureStatus.AlmostFull;
        }

        if (MinimumReached(slot))
        {
            return CrmDepartureStatus.Confirmed;
        }

        if (slot.TotalSeats > 0 && booked * 100 >= options.FillingFastPercent * slot.TotalSeats && booked > 0)
        {
            return CrmDepartureStatus.FillingFast;
        }

        return CrmDepartureStatus.OpenForBooking;
    }
}
