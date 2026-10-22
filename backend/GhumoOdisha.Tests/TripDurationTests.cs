using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Application.Trips.Validators;
using GhumoOdisha.Domain.Entities;

namespace GhumoOdisha.Tests;

public class TripDurationTests
{
    [Fact]
    public void Duration_IsCountedFromTheDates_UnlessTheAdminSetsIt()
    {
        var start = new DateOnly(2026, 11, 16);
        var end = new DateOnly(2026, 11, 19);

        Assert.Equal("4 Days / 3 Nights", TripDuration.Label(new Trip(), start, end));

        // Departs on the night of the 16th: the admin says what the trip really is.
        Assert.Equal("2 Days / 3 Nights", TripDuration.Label(new Trip { DurationDays = 2, DurationNights = 3 }, start, end));
        Assert.Equal("1 Day / 0 Nights", TripDuration.Label(new Trip(), start, start));

        // Only one of the two set = ignored (the validator stops that anyway).
        Assert.Equal("4 Days / 3 Nights", TripDuration.Label(new Trip { DurationDays = 2 }, start, end));
    }

    [Fact]
    public void Validator_NeedsBothOrNeither_AndAtMostOneMoreNightThanDays()
    {
        var v = new CreateTripRequestValidator();
        CreateTripRequest R(int? d, int? n) => new("Trip", "Desc", 1000m, false, false, false, false, false, DurationDays: d, DurationNights: n);

        Assert.True(v.Validate(R(null, null)).IsValid);
        Assert.True(v.Validate(R(2, 2)).IsValid);
        Assert.True(v.Validate(R(2, 3)).IsValid);
        Assert.False(v.Validate(R(2, null)).IsValid);
        Assert.False(v.Validate(R(2, 4)).IsValid);
        Assert.False(v.Validate(R(0, 0)).IsValid);
    }
}
