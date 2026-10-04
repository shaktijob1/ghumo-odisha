using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

/// <summary>Admin editing a departure's total seats keeps the seats already booked intact.</summary>
public class DateSlotSeatEditTests
{
    private static async Task<TripDateSlot> SeedSlotAsync(GhumoOdishaDbContext db, int totalSeats, int availableSeats)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Seat Edit Trip {Guid.NewGuid():N}", Description = "Seat edit test", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = DateOnly.FromDateTime(now.AddDays(30)),
            EndDate = DateOnly.FromDateTime(now.AddDays(32)),
            TotalSeats = totalSeats,
            AvailableSeats = availableSeats,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot;
    }

    private static UpdateDateSlotRequest Request(TripDateSlot slot, int totalSeats) =>
        new(slot.StartDate, slot.EndDate, totalSeats, slot.Status);

    [Fact]
    public async Task IncreasingTotalSeats_AddsTheSameNumberOfAvailableSeats()
    {
        await using var db = TestDb.CreateContext();
        var slot = await SeedSlotAsync(db, totalSeats: 10, availableSeats: 6);

        await new TripService(db, null!).UpdateDateSlotAsync(slot.TripDateSlotId, Request(slot, 15));

        var saved = await db.TripDateSlots.AsNoTracking().SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(15, saved.TotalSeats);
        Assert.Equal(11, saved.AvailableSeats);
    }

    [Fact]
    public async Task ReducingTotalSeats_DownToBookedSeats_LeavesNoneAvailable()
    {
        await using var db = TestDb.CreateContext();
        var slot = await SeedSlotAsync(db, totalSeats: 10, availableSeats: 6);

        await new TripService(db, null!).UpdateDateSlotAsync(slot.TripDateSlotId, Request(slot, 4));

        var saved = await db.TripDateSlots.AsNoTracking().SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(4, saved.TotalSeats);
        Assert.Equal(0, saved.AvailableSeats);
    }

    [Fact]
    public async Task ReducingTotalSeats_BelowBookedSeats_IsRejectedAndUnchanged()
    {
        await using var db = TestDb.CreateContext();
        var slot = await SeedSlotAsync(db, totalSeats: 10, availableSeats: 6);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new TripService(db, null!).UpdateDateSlotAsync(slot.TripDateSlotId, Request(slot, 3)));

        var saved = await db.TripDateSlots.AsNoTracking().SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(10, saved.TotalSeats);
        Assert.Equal(6, saved.AvailableSeats);
    }
}
