using GhumoOdisha.Application.Cars;

namespace GhumoOdisha.Tests;

/// <summary>
/// The Cars fare rules: base fare by km tier (falling until the last, open-ended tier),
/// per-km charge (no minimum km), night halts and the India-calendar night count.
/// </summary>
public class CarFareCalculatorTests
{
    // A 5-seater priced the way the business described: base fare falls every 50 km until 200 km,
    // then stays flat. ₹13/km, ₹400 night halt.
    private static readonly FareTerms Sedan = new(
        PricePerKm: 13m,
        NightHaltPrice: 400m,
        Tiers:
        [
            new FareTier(50, 800m),
            new FareTier(100, 600m),
            new FareTier(150, 450m),
            new FareTier(200, 300m),
            new FareTier(null, 300m),
        ]);

    [Theory]
    [InlineData(50, 800)]
    [InlineData(51, 600)]
    [InlineData(100, 600)]
    [InlineData(150, 450)]
    [InlineData(200, 300)]
    [InlineData(250, 300)]
    [InlineData(1000, 300)]
    public void Base_fare_falls_by_tier_and_stays_flat_after_the_last_limit(int km, decimal expectedBaseFare)
    {
        Assert.Equal(expectedBaseFare, CarFareCalculator.Calculate(Sedan, km, 0).BaseFare);
    }

    [Fact]
    public void Short_trip_pays_only_its_km_plus_the_higher_base_fare_no_minimum_km()
    {
        var fare = CarFareCalculator.Calculate(Sedan, km: 20, nights: 0);

        Assert.Equal(20, fare.Km);
        Assert.Equal(800m, fare.BaseFare);
        Assert.Equal(260m, fare.KmCharge);          // 20 × 13 — not topped up to any minimum
        Assert.Equal(1060m, fare.Total);
    }

    [Fact]
    public void Total_adds_base_km_night_halts_and_extras()
    {
        var fare = CarFareCalculator.Calculate(Sedan, km: 180, nights: 1, additionalCharges: 150m);

        Assert.Equal(300m, fare.BaseFare);
        Assert.Equal(2340m, fare.KmCharge);        // 180 × 13
        Assert.Equal(400m, fare.NightHaltCharge);
        Assert.Equal(150m, fare.AdditionalCharges);
        Assert.Equal(3190m, fare.Total);
    }

    [Fact]
    public void Tempo_traveller_with_a_single_flat_base_fare()
    {
        var tempo = new FareTerms(20m, 500m, [new FareTier(null, 1000m)]);

        var fare = CarFareCalculator.Calculate(tempo, km: 120, nights: 2);

        Assert.Equal(1000m + 120 * 20m + 2 * 500m, fare.Total);
    }

    [Fact]
    public void Tier_order_in_the_list_does_not_matter()
    {
        var shuffled = Sedan with { Tiers = [.. Sedan.Tiers.Reverse()] };

        Assert.Equal(600m, CarFareCalculator.Calculate(shuffled, 75, 0).BaseFare);
    }

    [Fact]
    public void Negative_inputs_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CarFareCalculator.Calculate(Sedan, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CarFareCalculator.Calculate(Sedan, 10, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => CarFareCalculator.Calculate(Sedan, 10, 0, -5m));
    }

    [Fact]
    public void Tier_validation_requires_exactly_one_open_ended_tier_and_distinct_limits()
    {
        Assert.Empty(CarFareCalculator.ValidateTiers(Sedan.Tiers.ToList()));
        Assert.NotEmpty(CarFareCalculator.ValidateTiers([]));
        Assert.NotEmpty(CarFareCalculator.ValidateTiers([new FareTier(100, 500m)]));
        Assert.NotEmpty(CarFareCalculator.ValidateTiers([new FareTier(null, 500m), new FareTier(null, 400m)]));
        Assert.NotEmpty(CarFareCalculator.ValidateTiers([new FareTier(100, 500m), new FareTier(100, 400m), new FareTier(null, 300m)]));
        Assert.NotEmpty(CarFareCalculator.ValidateTiers([new FareTier(0, 500m), new FareTier(null, 300m)]));
        Assert.NotEmpty(CarFareCalculator.ValidateTiers([new FareTier(null, -1m)]));
    }

    [Theory]
    // pickup (India time), duration hours, expected nights
    [InlineData("2026-10-02 10:00", 12, 0)]   // 10 AM → 10 PM same day
    [InlineData("2026-10-02 10:00", 24, 1)]   // crosses one midnight
    [InlineData("2026-10-02 18:00", 12, 1)]   // 6 PM → 6 AM
    [InlineData("2026-10-02 12:00", 12, 0)]   // ends exactly at midnight — not a night
    [InlineData("2026-10-02 10:00", 72, 3)]
    [InlineData("2026-10-02 00:00", 24, 0)]   // midnight → midnight is one calendar day
    public void Nights_are_counted_on_the_india_calendar(string pickupIndia, int hours, int expectedNights)
    {
        var local = DateTime.Parse(pickupIndia);
        var startUtc = CarRentalCalendar.IndiaToUtc(DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));

        Assert.Equal(expectedNights, CarRentalCalendar.NightsSpanned(startUtc, startUtc.AddHours(hours)));
    }

    [Fact]
    public void India_time_converts_to_utc_with_the_5h30_offset()
    {
        var utc = CarRentalCalendar.IndiaToUtc(new DateOnly(2026, 10, 2), new TimeOnly(10, 0));

        Assert.Equal(new DateTime(2026, 10, 2, 4, 30, 0), utc);
        Assert.Equal(new DateTime(2026, 10, 2, 10, 0, 0), CarRentalCalendar.UtcToIndia(utc));
    }
}
