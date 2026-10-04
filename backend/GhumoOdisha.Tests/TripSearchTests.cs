using GhumoOdisha.Application.Trips;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Tests.Fixtures;

namespace GhumoOdisha.Tests;

/// <summary>A trip is found by any place it covers, even when the visitor misspells it a little.</summary>
public class TripSearchTests
{
    [Theory]
    [InlineData("jirang")]
    [InlineData("Jiranga")]
    [InlineData("jirang monastery")]
    [InlineData("monastery")]
    [InlineData("gandahati")]
    [InlineData("gandhati")]
    [InlineData("mahendragri")]
    [InlineData("MAHENDRA")]
    public void Matches_PlaceNames_IncludingSmallMisspellings(string query)
    {
        Assert.True(TripSearch.Matches(query, ["Mahendragiri . Daringbadi", "Jirang Monastery", "Gandahati Waterfall"]));
    }

    [Theory]
    [InlineData("puri")]
    [InlineData("araku")]
    [InlineData("koraput")]
    public void DoesNotMatch_UnrelatedPlaces(string query)
    {
        Assert.False(TripSearch.Matches(query, ["Mahendragiri . Daringbadi", "Jirang Monastery", "Gandahati Waterfall"]));
    }

    [Fact]
    public void CleanPlaces_TrimsAndDropsBlanksAndRepeats()
    {
        var places = TripSearch.CleanPlaces([" Jirang Monastery ", "", "jirang monastery", "Gandahati Waterfall\nKhasada Waterfall"]);
        Assert.Equal(["Jirang Monastery", "Gandahati Waterfall", "Khasada Waterfall"], places);
    }

    [Fact]
    public async Task ActiveTripSearch_FindsTripByAPlaceItCovers()
    {
        await using var db = TestDb.CreateContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Title = $"Hills Escape {tag}",
            Description = "Places covered search test",
            AmountPerPerson = 1000m,
            Status = TripStatus.Active,
            PlacesCovered = TripSearch.JoinPlaces([$"Jirang{tag} Monastery", "Gandahati Waterfall"]),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var service = new TripService(db, null!);
        var found = await service.GetActiveTripsAsync(1, 50, search: $"jirang{tag}a");
        Assert.Contains(found.Items, t => t.TripId == trip.TripId);

        var detail = await service.GetTripDetailAsync(trip.TripId);
        Assert.Equal([$"Jirang{tag} Monastery", "Gandahati Waterfall"], detail.PlacesCovered);
    }
}
