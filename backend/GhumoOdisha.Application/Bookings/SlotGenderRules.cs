using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Bookings;

/// <summary>Gents / ladies already booked on one departure (confirmed and completed bookings).</summary>
public readonly record struct SlotGenderCount(int Gents, int Ladies);

/// <summary>
/// Gents / ladies booked on each departure, from the male / female numbers stored on each paid
/// booking — shown on the trip page's dates. There is no gender cap: anyone can book any open seat.
/// </summary>
public static class SlotGenderRules
{
    /// <summary>Booked gents / ladies per slot. Bookings without a male/female split (older ones) count toward neither.</summary>
    public static async Task<Dictionary<int, SlotGenderCount>> BookedAsync(
        IGhumoOdishaDbContext db, List<int> slotIds, int? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        if (slotIds.Count == 0)
        {
            return [];
        }

        var rows = await db.Bookings.AsNoTracking()
            .Where(b => b.TripDateSlotId != null && slotIds.Contains(b.TripDateSlotId.Value)
                     && (b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.Completed)
                     && (excludeBookingId == null || b.BookingId != excludeBookingId))
            .GroupBy(b => b.TripDateSlotId!.Value)
            .Select(g => new { SlotId = g.Key, Gents = g.Sum(b => b.MaleCount ?? 0), Ladies = g.Sum(b => b.FemaleCount ?? 0) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.SlotId, r => new SlotGenderCount(r.Gents, r.Ladies));
    }
}
