using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Tests.Fixtures;

namespace GhumoOdisha.Tests;

public class TripListingTests
{
    [Fact]
    public async Task ActiveTrips_AreOrderedBySoonestUpcomingDeparture_NotByTitle()
    {
        await using var db = TestDb.CreateContext();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var today = TripCalendar.Today();
        var now = DateTime.UtcNow;

        // Alphabetical order (A, M, Z) is deliberately the opposite of departure order.
        Trip AddTrip(string title, int? daysAhead)
        {
            var trip = new Trip { Title = $"{title} {tag}", Description = "Listing order test", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
            db.Trips.Add(trip);
            if (daysAhead is { } d)
            {
                trip.TripDateSlots.Add(new TripDateSlot
                {
                    StartDate = today.AddDays(d), EndDate = today.AddDays(d + 1), TotalSeats = 10, AvailableSeats = 10,
                    Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now
                });
            }
            // A past departure must not count as "next".
            trip.TripDateSlots.Add(new TripDateSlot
            {
                StartDate = today.AddDays(-30), EndDate = today.AddDays(-29), TotalSeats = 10, AvailableSeats = 10,
                Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now
            });
            return trip;
        }

        AddTrip("Aaa late", 40);
        AddTrip("Mmm no upcoming dates", null);
        AddTrip("Zzz soon", 2);
        await db.SaveChangesAsync();

        var service = new TripService(db, null!);
        var result = await service.GetActiveTripsAsync(1, 10, search: tag);

        Assert.Equal(
            [$"Zzz soon {tag}", $"Aaa late {tag}", $"Mmm no upcoming dates {tag}"],
            result.Items.Select(t => t.Title).ToArray());
    }
}
