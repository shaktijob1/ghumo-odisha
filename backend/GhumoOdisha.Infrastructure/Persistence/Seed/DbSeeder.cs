using GhumoOdisha.Application.Auth;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(GhumoOdishaDbContext context, IPinHasher pinHasher)
    {
        var now = DateTime.UtcNow;

        const string adminUsername = "gobook";
        const string adminPassword = "gobook";

        var existingAdmin = await context.AdminUsers.FirstOrDefaultAsync();
        if (existingAdmin is null)
        {
            context.AdminUsers.Add(new AdminUser
            {
                Username = adminUsername,
                Email = "admin@ghumoodisha.in",
                PasswordHash = pinHasher.Hash(adminPassword),
                Role = "Admin",
                CreatedAt = now
            });

            await context.SaveChangesAsync();
        }
        else if (existingAdmin.Username != adminUsername)
        {
            existingAdmin.Username = adminUsername;
            existingAdmin.PasswordHash = pinHasher.Hash(adminPassword);
            await context.SaveChangesAsync();
        }

        if (await context.Trips.AnyAsync())
        {
            return;
        }

        context.Trips.AddRange(
            BuildKoraputEscape(now),
            BuildPuriKonarkEscape(now),
            BuildMahendragiriAdventure(now));

        await context.SaveChangesAsync();
    }

    private static Trip BuildKoraputEscape(DateTime now)
    {
        var trip = new Trip
        {
            Title = "Koraput Escape",
            Description = "Two nights in the Koraput hills — sunrise at Deomali, the open ridge at Talamali, "
                + "a forest walk to Punjisil, and the Gupteswar caves on the way back. Overnight bus both ways "
                + "from Bhubaneswar, so you only spend two working days away.",
            AmountPerPerson = 4999.00m,
            IncludesBreakfast = true,
            IncludesLunch = false,
            IncludesDinner = true,
            IncludesStay = true,
            IncludesCoordinator = true,
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/koraput-hero.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/koraput-valley.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/koraput-waterfall.jpg", DisplayOrder = 2, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/koraput-temple.jpg", DisplayOrder = 3, CreatedAt = now });

        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Deomali", Description = "Odisha's highest peak, best reached before the mist lifts at 7 am.", PhotoUrl = "/uploads/seed/highlight-deomali.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Talamali", Description = "Open grassland ridge with valley views on both sides.", PhotoUrl = "/uploads/seed/highlight-talamali.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Punjisil Waterfall", Description = "A short forest walk down to the pool at the base.", PhotoUrl = "/uploads/seed/highlight-punjisil.jpg", DisplayOrder = 2, CreatedAt = now });

        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 9, 16), EndDate = new DateOnly(2026, 9, 18), TotalSeats = 16, AvailableSeats = 10, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });
        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 9, 23), EndDate = new DateOnly(2026, 9, 25), TotalSeats = 16, AvailableSeats = 16, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });
        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 9, 30), EndDate = new DateOnly(2026, 10, 2), TotalSeats = 16, AvailableSeats = 0, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });

        var day1 = new ItineraryDay { DayNumber = 1, Title = "Bhubaneswar → Koraput & sightseeing", Description = "Overnight departure, arrival in Koraput, and the day's two big highlights.", DisplayOrder = 0 };
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "08:00 PM", Description = "Depart from Jaydev Vihar", DisplayOrder = 0 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "06:00 AM", Description = "Reach Koraput", DisplayOrder = 1 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "08:00 AM", Description = "Visit Deomali", DisplayOrder = 2 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "10:30 AM", Description = "Explore Talamali", DisplayOrder = 3 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "07:30 PM", Description = "Dinner and check-in", DisplayOrder = 4 });
        trip.ItineraryDays.Add(day1);

        var day2 = new ItineraryDay { DayNumber = 2, Title = "Waterfalls, Gupteswar & return journey", Description = "Punjisil trek, the Gupteswar caves, and the ride back to Bhubaneswar.", DisplayOrder = 1 };
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "07:00 AM", Description = "Breakfast at the stay", DisplayOrder = 0 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "09:00 AM", Description = "Punjisil Waterfall trek", DisplayOrder = 1 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "01:00 PM", Description = "Gupteswar Caves", DisplayOrder = 2 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "05:00 PM", Description = "Begin return journey", DisplayOrder = 3 });
        trip.ItineraryDays.Add(day2);

        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/koraput-room-1.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/koraput-room-2.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/koraput-room-view.jpg", DisplayOrder = 2, CreatedAt = now });

        return trip;
    }

    private static Trip BuildPuriKonarkEscape(DateTime now)
    {
        var trip = new Trip
        {
            Title = "Puri Konark Escape",
            Description = "A single overnight trip to the Odisha coast — sunrise at Puri beach, the Jagannath "
                + "Temple, and the Sun Temple at Konark on the way back.",
            AmountPerPerson = 3499.00m,
            IncludesBreakfast = true,
            IncludesLunch = false,
            IncludesDinner = false,
            IncludesStay = true,
            IncludesCoordinator = true,
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/puri-hero.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/puri-beach.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/konark-temple.jpg", DisplayOrder = 2, CreatedAt = now });

        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Puri Beach", Description = "Golden sand and a sunrise worth the early alarm.", PhotoUrl = "/uploads/seed/highlight-puri-beach.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Jagannath Temple", Description = "One of the Char Dham, the spiritual heart of Puri.", PhotoUrl = "/uploads/seed/highlight-jagannath.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Konark Sun Temple", Description = "A UNESCO World Heritage chariot carved entirely in stone.", PhotoUrl = "/uploads/seed/highlight-konark.jpg", DisplayOrder = 2, CreatedAt = now });

        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 9, 20), EndDate = new DateOnly(2026, 9, 21), TotalSeats = 16, AvailableSeats = 16, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });
        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 9, 27), EndDate = new DateOnly(2026, 9, 28), TotalSeats = 16, AvailableSeats = 12, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });

        var day1 = new ItineraryDay { DayNumber = 1, Title = "Bhubaneswar → Puri sightseeing", Description = "Morning departure, temple darshan, and a beach sunset.", DisplayOrder = 0 };
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "06:00 AM", Description = "Depart from Jaydev Vihar", DisplayOrder = 0 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "09:00 AM", Description = "Reach Puri, check-in", DisplayOrder = 1 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "10:00 AM", Description = "Jagannath Temple darshan", DisplayOrder = 2 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "04:00 PM", Description = "Puri Beach sunset walk", DisplayOrder = 3 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "08:00 PM", Description = "Dinner at the stay", DisplayOrder = 4 });
        trip.ItineraryDays.Add(day1);

        var day2 = new ItineraryDay { DayNumber = 2, Title = "Konark & return journey", Description = "Sunrise at the beach, the Sun Temple, and the ride home.", DisplayOrder = 1 };
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "06:00 AM", Description = "Sunrise at Puri Beach", DisplayOrder = 0 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "09:00 AM", Description = "Depart for Konark", DisplayOrder = 1 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "10:30 AM", Description = "Konark Sun Temple", DisplayOrder = 2 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "02:00 PM", Description = "Begin return journey", DisplayOrder = 3 });
        trip.ItineraryDays.Add(day2);

        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/puri-room-1.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/puri-room-2.jpg", DisplayOrder = 1, CreatedAt = now });

        return trip;
    }

    private static Trip BuildMahendragiriAdventure(DateTime now)
    {
        var trip = new Trip
        {
            Title = "Mahendragiri Adventure",
            Description = "A trek through the Eastern Ghats to Mahendragiri — waterfalls, dense forest trails, "
                + "and an overnight camp under the stars.",
            AmountPerPerson = 5999.00m,
            IncludesBreakfast = true,
            IncludesLunch = false,
            IncludesDinner = true,
            IncludesStay = true,
            IncludesCoordinator = true,
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/mahendragiri-hero.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/mahendragiri-trail.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.TripPhotos.Add(new TripPhoto { ImageUrl = "/uploads/seed/mahendragiri-camp.jpg", DisplayOrder = 2, CreatedAt = now });

        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Mahendragiri Peak", Description = "The second-highest peak in Odisha, wrapped in ancient forest.", PhotoUrl = "/uploads/seed/highlight-mahendragiri-peak.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Forest Trail", Description = "A dense, shaded trek through Eastern Ghats woodland.", PhotoUrl = "/uploads/seed/highlight-forest-trail.jpg", DisplayOrder = 1, CreatedAt = now });
        trip.TripHighlights.Add(new TripHighlight { PlaceName = "Camp Under the Stars", Description = "An overnight halt with a bonfire and clear night skies.", PhotoUrl = "/uploads/seed/highlight-camp.jpg", DisplayOrder = 2, CreatedAt = now });

        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 9, 27), EndDate = new DateOnly(2026, 9, 29), TotalSeats = 12, AvailableSeats = 4, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });
        trip.TripDateSlots.Add(new TripDateSlot { StartDate = new DateOnly(2026, 10, 4), EndDate = new DateOnly(2026, 10, 6), TotalSeats = 12, AvailableSeats = 12, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now });

        var day1 = new ItineraryDay { DayNumber = 1, Title = "Bhubaneswar → Mahendragiri base camp", Description = "Overnight departure, arrival at the base village, and the trek in to camp.", DisplayOrder = 0 };
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "09:00 PM", Description = "Depart from Jaydev Vihar", DisplayOrder = 0 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "07:00 AM", Description = "Reach base village", DisplayOrder = 1 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "09:00 AM", Description = "Trek begins", DisplayOrder = 2 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "01:00 PM", Description = "Lunch break at the mid-point clearing", DisplayOrder = 3 });
        day1.ItineraryPoints.Add(new ItineraryPoint { Time = "06:00 PM", Description = "Reach camp, bonfire dinner", DisplayOrder = 4 });
        trip.ItineraryDays.Add(day1);

        var day2 = new ItineraryDay { DayNumber = 2, Title = "Summit trek & waterfalls", Description = "The summit push, a return to camp, and an afternoon waterfall visit.", DisplayOrder = 1 };
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "06:00 AM", Description = "Sunrise trek to the summit", DisplayOrder = 0 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "11:00 AM", Description = "Return to camp", DisplayOrder = 1 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "02:00 PM", Description = "Waterfall visit", DisplayOrder = 2 });
        day2.ItineraryPoints.Add(new ItineraryPoint { Time = "08:00 PM", Description = "Overnight camp", DisplayOrder = 3 });
        trip.ItineraryDays.Add(day2);

        var day3 = new ItineraryDay { DayNumber = 3, Title = "Return journey", Description = "Pack up camp, trek back to the base village, and head home.", DisplayOrder = 2 };
        day3.ItineraryPoints.Add(new ItineraryPoint { Time = "06:00 AM", Description = "Breakfast and pack up", DisplayOrder = 0 });
        day3.ItineraryPoints.Add(new ItineraryPoint { Time = "08:00 AM", Description = "Trek back to base village", DisplayOrder = 1 });
        day3.ItineraryPoints.Add(new ItineraryPoint { Time = "12:00 PM", Description = "Begin return journey to Bhubaneswar", DisplayOrder = 2 });
        trip.ItineraryDays.Add(day3);

        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/mahendragiri-camp-1.jpg", DisplayOrder = 0, CreatedAt = now });
        trip.RoomPhotos.Add(new RoomPhoto { ImageUrl = "/uploads/seed/mahendragiri-camp-2.jpg", DisplayOrder = 1, CreatedAt = now });

        return trip;
    }
}
