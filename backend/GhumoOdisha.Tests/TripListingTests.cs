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

        var trips = new[] { AddTrip("Aaa late", 40), AddTrip("Mmm no upcoming dates", null), AddTrip("Zzz soon", 2) };
        await db.SaveChangesAsync();

        try
        {
            var service = new TripService(db, null!);
            var result = await service.GetActiveTripsAsync(1, 10, search: tag);

            Assert.Equal(
                [$"Zzz soon {tag}", $"Aaa late {tag}", $"Mmm no upcoming dates {tag}"],
                result.Items.Select(t => t.Title).ToArray());
        }
        finally
        {
            // The tests share the dev database — don't leave these trips on the home page.
            db.Trips.RemoveRange(trips);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UpcomingTripLocations_ListEveryPlaceWithABookableTrip_AndOnlyThose()
    {
        await using var db = TestDb.CreateContext();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var today = TripCalendar.Today();
        var now = DateTime.UtcNow;

        Destination AddPlace(string name)
        {
            var place = new Destination { Name = $"{name} {tag}", Slug = $"{name.ToLowerInvariant()}-{tag}", IsActive = true, CreatedAt = now, UpdatedAt = now };
            db.Destinations.Add(place);
            return place;
        }

        Trip AddTrip(Destination place, int daysAhead, TripStatus status = TripStatus.Active)
        {
            var trip = new Trip { Title = $"Locations test {tag}", Description = "Locations test", AmountPerPerson = 1000m, Status = status, CreatedAt = now, UpdatedAt = now };
            trip.Destinations.Add(place);
            trip.TripDateSlots.Add(new TripDateSlot
            {
                StartDate = today.AddDays(daysAhead), EndDate = today.AddDays(daysAhead + 1), TotalSeats = 10, AvailableSeats = 10,
                Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now
            });
            db.Trips.Add(trip);
            return trip;
        }

        var soon = AddPlace("Soon");
        var later = AddPlace("Later");
        var past = AddPlace("Past");
        var hidden = AddPlace("Hidden");
        var trips = new[]
        {
            AddTrip(soon, 3),
            AddTrip(later, 60),
            AddTrip(past, -10),                    // only a past departure → not bookable
            AddTrip(hidden, 5, TripStatus.Inactive), // switched off by the admin
        };
        await db.SaveChangesAsync();

        try
        {
            var service = new TripService(db, null!);

            var all = (await service.GetUpcomingTripLocationsAsync()).Where(n => n.EndsWith(tag)).ToArray();
            Assert.Equal([$"Later {tag}", $"Soon {tag}"], all);

            // A date range (the searched month) narrows the list to places with a trip in it.
            var nearTerm = (await service.GetUpcomingTripLocationsAsync(today, today.AddDays(10))).Where(n => n.EndsWith(tag)).ToArray();
            Assert.Equal([$"Soon {tag}"], nearTerm);
        }
        finally
        {
            db.Trips.RemoveRange(trips);
            db.Destinations.RemoveRange(soon, later, past, hidden);
            await db.SaveChangesAsync();
        }
    }
}
