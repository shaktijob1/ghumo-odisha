using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Maps;
using GhumoOdisha.Domain.Entities;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Cars;

/// <summary>
/// Billed km for a vehicle trip — the whole distance the vehicle drives, from leaving the driver's base
/// to getting back to it — all measured by road on the server. "Drop" is the customer's "where to":
///
///   one way:     base → pickup + pickup → drop + drop → base
///   round trip:  base → pickup + pickup → drop + drop → pickup + pickup → base
/// </summary>
public record CarRoutePlan(
    bool IsAvailable,
    string? UnavailableReason,
    bool RoundTrip,
    int DriverApproachKm,
    int PickupToDropKm,
    /// <summary>Round trip only (0 one way): the drive back from "where to" to the pickup.</summary>
    int DropToPickupKm,
    /// <summary>Back to the driver's base — from the drop (one way) or from the pickup (round trip).</summary>
    int ReturnToBaseKm,
    int TotalKm)
{
    public static CarRoutePlan Unavailable(string reason) => new(false, reason, false, 0, 0, 0, 0, 0);
}

/// <summary>The legs that don't depend on which vehicle is booked — measured once per search.</summary>
public record CarTripLegs(GeoPoint Pickup, GeoPoint Drop, bool RoundTrip, int PickupToDropKm, int DropToPickupKm);

public interface ICarRoutePlanner
{
    /// <summary>Area check + the customer's own legs; null with a reason when the pickup isn't served.</summary>
    Task<(CarTripLegs? Legs, string? UnavailableReason)> LegsAsync(TripPlaceRequest pickup, TripPlaceRequest drop, bool roundTrip, CancellationToken cancellationToken = default);

    /// <summary>Adds one driver's km from their base to the pickup and back to their base at the end.</summary>
    Task<CarRoutePlan> ForDriverAsync(CarTripLegs legs, Driver driver, CancellationToken cancellationToken = default);

    Task<CarRoutePlan> PlanAsync(Driver driver, TripPlaceRequest pickup, TripPlaceRequest drop, bool roundTrip, CancellationToken cancellationToken = default);
}

public class CarRoutePlanner(IServiceAreaService serviceAreas, IMapsService maps, IOptions<CarRentalOptions> options) : ICarRoutePlanner
{
    public const string NoDriverBaseMessage = "This vehicle can't be booked online right now. Please choose another one.";

    public async Task<(CarTripLegs? Legs, string? UnavailableReason)> LegsAsync(
        TripPlaceRequest pickup, TripPlaceRequest drop, bool roundTrip, CancellationToken cancellationToken = default)
    {
        var from = new GeoPoint(pickup.Latitude, pickup.Longitude);
        var to = new GeoPoint(drop.Latitude, drop.Longitude);
        if (!from.IsValid || !to.IsValid)
        {
            throw new ValidationAppException(["Choose the pickup and where you're going from the suggestions."]);
        }

        var check = await serviceAreas.CheckAsync(from, cancellationToken);
        if (!check.IsServiceable)
        {
            return (null, check.Message ?? ServiceAreaService.OutsideAreaMessage);
        }

        // Roads aren't always the same length both ways (one-way streets), so the way back is measured too.
        var outTask = maps.DrivingDistanceMetersAsync(from, to, cancellationToken);
        var backTask = roundTrip ? maps.DrivingDistanceMetersAsync(to, from, cancellationToken) : Task.FromResult(0);
        await Task.WhenAll(outTask, backTask);

        return (new CarTripLegs(from, to, roundTrip, GeoMath.MetresToKm(outTask.Result), GeoMath.MetresToKm(backTask.Result)), null);
    }

    public async Task<CarRoutePlan> ForDriverAsync(CarTripLegs legs, Driver driver, CancellationToken cancellationToken = default)
    {
        if (driver.BaseLatitude is not { } baseLat || driver.BaseLongitude is not { } baseLng)
        {
            return CarRoutePlan.Unavailable(NoDriverBaseMessage);
        }
        var home = new GeoPoint(baseLat, baseLng);

        // The trip ends at "where to" (one way) or back at the pickup (round trip); the driver drives home from there.
        var end = legs.RoundTrip ? legs.Pickup : legs.Drop;
        var approachTask = maps.DrivingDistanceMetersAsync(home, legs.Pickup, cancellationToken);
        var returnTask = maps.DrivingDistanceMetersAsync(end, home, cancellationToken);
        await Task.WhenAll(approachTask, returnTask);

        var approach = GeoMath.MetresToKm(approachTask.Result);
        var back = GeoMath.MetresToKm(returnTask.Result);
        var total = approach + legs.PickupToDropKm + legs.DropToPickupKm + back;

        if (total < 1)
        {
            throw new ValidationAppException(["Choose where you're going."]);
        }
        if (total > options.Value.MaxEstimatedKm)
        {
            throw new ValidationAppException([$"That trip is too long to book online (over {options.Value.MaxEstimatedKm:N0} km). Please contact us."]);
        }

        return new CarRoutePlan(true, null, legs.RoundTrip, approach, legs.PickupToDropKm, legs.DropToPickupKm, back, total);
    }

    public async Task<CarRoutePlan> PlanAsync(Driver driver, TripPlaceRequest pickup, TripPlaceRequest drop, bool roundTrip, CancellationToken cancellationToken = default)
    {
        var (legs, reason) = await LegsAsync(pickup, drop, roundTrip, cancellationToken);
        return legs is null ? CarRoutePlan.Unavailable(reason!) : await ForDriverAsync(legs, driver, cancellationToken);
    }
}
