using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Maps;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

/// <summary>
/// Map-based vehicle fares: the pickup must be inside an admin service area (drawn zone or PIN code), and
/// the billed km are measured on the server — driver's base → pickup + pickup → drop + drop → driver's base.
/// </summary>
[Collection("Cars")]
public class CarLocationTests : IDisposable
{
    private readonly CarTestData _data = new();
    private readonly List<int> _areaIds = [];

    private static CarQuoteRequest Quote(TripPlaceRequest pickup, TripPlaceRequest drop, int daysAhead = 0) =>
        new(CarTestData.PickupDate.AddDays(daysAhead), CarTestData.TenAm, 12, pickup, drop);

    [Fact]
    public async Task Billed_km_are_driver_to_pickup_plus_pickup_to_drop_plus_drop_back_to_the_driver_and_are_frozen_on_the_booking()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await _data.ListedCarAsync(db);
        var customer = await _data.CustomerAsync(db);

        // Base → pickup 10 km, pickup → drop 40 km, drop → base 50 km.
        var quote = await CarTestData.Catalog(db).QuoteAsync(carId, Quote(CarTestData.Pickup, CarTestData.DropAt(40)));

        Assert.True(quote.IsAvailable);
        Assert.Equal(10, quote.DriverApproachKm);
        Assert.Equal(40, quote.PickupToDropKm);
        Assert.Equal(50, quote.ReturnToBaseKm);
        Assert.Equal(100, quote.EstimatedKm);
        Assert.Equal(500m + 100 * 15m, quote.EstimatedTotal);   // base ₹500 (≤100 km) + 100 km × ₹15

        var booking = await CarTestData.Bookings(db).CreateAsync(customer, new CreateCarBookingRequest(carId, CarTestData.PickupDate, CarTestData.TenAm, 12,
            CarTestData.Pickup, CarTestData.DropAt(40), false, "Room 12, Hotel Mayfair, Janpath", null, Guid.NewGuid()));

        Assert.Equal(quote.EstimatedTotal, booking.Estimate.Total);
        Assert.Equal(100, booking.Estimate.Km);
        Assert.Equal(10, booking.DriverApproachKm);
        Assert.Equal(40, booking.PickupToDropKm);
        Assert.Equal(50, booking.ReturnToBaseKm);
        Assert.Equal("Hotel Mayfair", booking.PickupLocation);
        Assert.Equal("40 km away", booking.DropLocation);
        Assert.Equal("Room 12, Hotel Mayfair, Janpath", booking.PickupAddress);
    }

    [Fact]
    public async Task Round_trip_charges_there_and_back_to_the_pickup_and_the_drive_home_from_the_pickup()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await _data.ListedCarAsync(db);
        var customer = await _data.CustomerAsync(db);
        // "Where to" sits halfway between the driver's base and the pickup: 5 km from each.
        var whereTo = new TripPlaceRequest(21.05, 80.00, "Halfway");

        var oneWay = await CarTestData.Catalog(db).QuoteAsync(carId, Quote(CarTestData.Pickup, whereTo));
        Assert.False(oneWay.RoundTrip);
        Assert.Equal(10 + 5 + 5, oneWay.EstimatedKm);            // base → pickup → where to → base
        Assert.Equal(0, oneWay.DropToPickupKm);

        var round = await CarTestData.Catalog(db).QuoteAsync(carId, Quote(CarTestData.Pickup, whereTo) with { RoundTrip = true });
        Assert.True(round.RoundTrip);
        Assert.Equal(10, round.DriverApproachKm);
        Assert.Equal(5, round.PickupToDropKm);
        Assert.Equal(5, round.DropToPickupKm);
        Assert.Equal(10, round.ReturnToBaseKm);                   // home from the pickup, not from "where to"
        Assert.Equal(30, round.EstimatedKm);

        var booking = await CarTestData.Bookings(db).CreateAsync(customer, new CreateCarBookingRequest(carId, CarTestData.PickupDate, CarTestData.TenAm, 12,
            CarTestData.Pickup, whereTo, true, "Room 12, Hotel Mayfair, Janpath", null, Guid.NewGuid()));
        Assert.True(booking.RoundTrip);
        Assert.Equal(5, booking.DropToPickupKm);
        Assert.Equal(round.EstimatedTotal, booking.Estimate.Total);
    }

    [Fact]
    public async Task Drop_at_the_pickup_still_charges_the_drivers_km_both_ways()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await _data.ListedCarAsync(db);

        var quote = await CarTestData.Catalog(db).QuoteAsync(carId, Quote(CarTestData.Pickup, CarTestData.Pickup));
        Assert.Equal(0, quote.PickupToDropKm);
        Assert.Equal(20, quote.EstimatedKm);
    }

    [Fact]
    public async Task Fare_search_prices_every_vehicle_and_lists_nothing_outside_the_service_areas()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await _data.ListedCarAsync(db);

        var results = await CarTestData.Catalog(db).SearchWithFaresAsync(new CarFareSearchRequest(
            CarTestData.PickupDate, CarTestData.TenAm, 12, CarTestData.Pickup, CarTestData.DropAt(40), false, null));
        Assert.True(results.IsServiceable);
        var mine = Assert.Single(results.Cars, c => c.Car.CarId == carId);
        Assert.NotNull(mine.Fare);
        Assert.Equal(100, mine.Fare!.EstimatedKm);
        Assert.Equal(500m + 100 * 15m, mine.Fare.EstimatedTotal);

        var outside = await CarTestData.Catalog(db).SearchWithFaresAsync(new CarFareSearchRequest(
            CarTestData.PickupDate, CarTestData.TenAm, 12, new TripPlaceRequest(-40, -60, "Far away"), CarTestData.DropAt(40), false, null));
        Assert.False(outside.IsServiceable);
        Assert.Empty(outside.Cars);
    }

    [Fact]
    public async Task Pickup_outside_every_service_area_is_unavailable_and_cannot_be_booked()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await _data.ListedCarAsync(db);
        var customer = await _data.CustomerAsync(db);
        var outside = new TripPlaceRequest(-40.0, -60.0, "Far away");   // South Atlantic — in no real zone either

        var check = await CarTestData.ServiceAreas(db).CheckAsync(new GeoPoint(outside.Latitude, outside.Longitude));
        Assert.False(check.IsServiceable);

        var quote = await CarTestData.Catalog(db).QuoteAsync(carId, Quote(outside, CarTestData.DropAt(40)));
        Assert.False(quote.IsAvailable);
        Assert.Equal(ServiceAreaService.OutsideAreaMessage, quote.UnavailableReason);
        Assert.Equal(0m, quote.EstimatedTotal);

        await Assert.ThrowsAsync<ConflictException>(() => CarTestData.Bookings(db).CreateAsync(customer,
            new CreateCarBookingRequest(carId, CarTestData.PickupDate, CarTestData.TenAm, 12, outside, CarTestData.DropAt(40), false,
                "Somewhere far, far away", null, Guid.NewGuid())));
    }

    [Fact]
    public async Task Pickup_is_serviceable_by_pin_code_even_outside_the_drawn_zones()
    {
        await using var db = TestDb.CreateContext();
        await _data.ListedCarAsync(db);   // makes sure at least one active area exists
        var point = new GeoPoint(-41.0, -61.0);
        var maps = new FakeMapsService();
        var service = CarTestData.ServiceAreas(db, maps);

        Assert.False((await service.CheckAsync(point)).IsServiceable);

        maps.PostalCodes[(point.Latitude, point.Longitude)] = "999001";
        var area = await service.CreateAsync(new SaveServiceAreaRequest("PIN test", true, null, [" 999001 ", "999002", "999001"]));
        _areaIds.Add(area.ServiceAreaId);
        Assert.Equal(["999001", "999002"], area.Pincodes);

        var check = await service.CheckAsync(point);
        Assert.True(check.IsServiceable);
        Assert.Equal("PIN test", check.AreaName);

        // Switched off → no longer counts.
        await service.UpdateAsync(area.ServiceAreaId, new SaveServiceAreaRequest("PIN test", false, null, ["999001"]));
        Assert.False((await service.CheckAsync(point)).IsServiceable);
    }

    [Fact]
    public async Task A_vehicle_whose_driver_has_no_starting_point_cannot_be_quoted()
    {
        await using var db = TestDb.CreateContext();
        var (carId, driverId) = await _data.ListedCarAsync(db);
        await db.Drivers.Where(d => d.DriverId == driverId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.BaseLatitude, (double?)null).SetProperty(d => d.BaseLongitude, (double?)null));

        var quote = await CarTestData.Catalog(db).QuoteAsync(carId, Quote(CarTestData.Pickup, CarTestData.DropAt(40)));
        Assert.False(quote.IsAvailable);
        Assert.Equal(CarRoutePlanner.NoDriverBaseMessage, quote.UnavailableReason);
    }

    [Fact]
    public void Point_in_polygon()
    {
        List<GeoPoint> square = [new(0, 0), new(0, 1), new(1, 1), new(1, 0)];
        Assert.True(GeoMath.IsInside(new GeoPoint(0.5, 0.5), square));
        Assert.False(GeoMath.IsInside(new GeoPoint(1.5, 0.5), square));
        Assert.False(GeoMath.IsInside(new GeoPoint(0.5, 0.5), square.Take(2).ToList()));
        Assert.Equal(2, GeoMath.MetresToKm(1200));
        Assert.Equal(0, GeoMath.MetresToKm(0));
    }

    public void Dispose()
    {
        using (var db = TestDb.CreateContext())
        {
            db.ServiceAreas.Where(a => _areaIds.Contains(a.ServiceAreaId)).ExecuteDelete();
        }
        _data.Dispose();
        GC.SuppressFinalize(this);
    }
}
