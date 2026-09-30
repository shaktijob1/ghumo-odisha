using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Bookings;

/// <summary>Gents / ladies already booked on one departure (confirmed and completed bookings).</summary>
public readonly record struct SlotGenderCount(int Gents, int Ladies);

/// <summary>
/// Group trips keep a 1:1 mix: on each departure at most half the seats (rounded up) go to gents and at
/// most half to ladies — first come, first served within that. The counts come from the male / female
/// numbers stored on each paid booking; the slot itself only keeps its total and available seats.
/// Website bookings are held to the limit; an admin's manual booking may go over it on purpose.
/// </summary>
public static class SlotGenderRules
{
    /// <summary>Most gents (and, separately, most ladies) a departure takes: 20 seats → 10, 15 seats → 8.</summary>
    public static int CapPerSide(int totalSeats) => (totalSeats + 1) / 2;

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

    /// <summary>Rejects a website booking that would take gents or ladies past half the departure's seats.</summary>
    public static async Task EnsureWithinAsync(
        IGhumoOdishaDbContext db, TripDateSlot slot, int gents, int ladies, int? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var booked = (await BookedAsync(db, [slot.TripDateSlotId], excludeBookingId, cancellationToken))
            .GetValueOrDefault(slot.TripDateSlotId);
        var cap = CapPerSide(slot.TotalSeats);
        var gentsLeft = Math.Max(cap - booked.Gents, 0);
        var ladiesLeft = Math.Max(cap - booked.Ladies, 0);

        var errors = new List<string>();
        if (gents > gentsLeft)
        {
            errors.Add(gentsLeft == 0 ? "Gents' places are full on this date." : $"Only {gentsLeft} gents' place(s) are left on this date.");
        }
        if (ladies > ladiesLeft)
        {
            errors.Add(ladiesLeft == 0 ? "Ladies' places are full on this date." : $"Only {ladiesLeft} ladies' place(s) are left on this date.");
        }
        if (errors.Count > 0)
        {
            throw new ConflictException(string.Join(" ", errors));
        }
    }
}
