using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

/// <summary>
/// The trip itself (Cars phase 5): start/end odometer, actual km, night halts capped by the calendar,
/// the final fare from the booking's own pricing, balance collection and audited admin corrections.
/// </summary>
[Collection("Cars")]
public class CarTripTests : IDisposable
{
    private readonly CarTestData _data = new();

    /// <summary>A paid booking whose pickup is 30 minutes away, so the driver may start it now.</summary>
    private async Task<(CarBookingDto Booking, int CustomerId, int DriverId, int CarId)> StartableBookingAsync(GhumoOdishaDbContext db, int km = 180)
    {
        var (carId, driverId) = await _data.ListedCarAsync(db);
        var customerId = await _data.CustomerAsync(db);
        var booking = await CarTestData.PaidBookingAsync(db, customerId, carId, new FakeRazorpayService(), km: km);
        var pickup = DateTime.UtcNow.AddMinutes(30);
        await db.CarBookings.Where(b => b.CarBookingId == booking.CarBookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.PickupAt, pickup).SetProperty(b => b.EndsAt, pickup.AddHours(12)));
        // Direct SQL updates bypass the change tracker — drop stale tracked copies, as a new request would.
        db.ChangeTracker.Clear();
        return (booking, customerId, driverId, carId);
    }

    /// <summary>Pretend the trip started <paramref name="daysAgo"/> days ago, so night halts become possible.</summary>
    private static async Task BackdateStartAsync(GhumoOdishaDbContext db, int bookingId, int daysAgo)
    {
        await db.CarTripExecutions.Where(e => e.CarBookingId == bookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.StartedAt, DateTime.UtcNow.AddDays(-daysAgo)));
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Full_trip_actual_km_drives_the_final_fare_and_the_estimate_is_kept()
    {
        await using var db = TestDb.CreateContext();
        var (booking, customerId, driverId, _) = await StartableBookingAsync(db, km: 180);
        var trips = CarTestData.Trips(db);

        var started = await trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(24520, 20.2961m, 85.8245m));
        Assert.Equal(CarBookingStatus.InProgress, started.Booking.Status);
        Assert.NotNull(started.CustomerPhone);

        // Customer sees the start straight away.
        var customerView = await CarTestData.Bookings(db).GetForCustomerAsync(customerId, booking.CarBookingId);
        Assert.Equal(24520, customerView.Trip!.StartOdometerKm);
        Assert.Contains(customerView.Timeline, t => t.Title == "Trip started" && t.Note == "Start KM 24,520");

        await BackdateStartAsync(db, booking.CarBookingId, daysAgo: 1);
        var end = new EndTripRequest(24770, 1, 150m, "Tolls", null, null);   // 250 km, 1 night, ₹150 tolls
        var preview = await trips.PreviewEndAsync(driverId, booking.CarBookingId, end);
        Assert.Equal(250, preview.ActualKm);
        Assert.Equal(300m + 250 * 15m + 400m + 150m, preview.Final.Total);
        Assert.Equal(preview.Final.Total - 99m, preview.BalanceDue);
        Assert.Equal(CarBookingStatus.InProgress, (await trips.GetForDriverAsync(driverId, booking.CarBookingId)).Booking.Status);   // preview saves nothing

        var completed = await trips.CompleteTripAsync(driverId, booking.CarBookingId, end);
        Assert.Equal(CarBookingStatus.Completed, completed.Booking.Status);
        Assert.Null(completed.CustomerPhone);

        var final = await CarTestData.Bookings(db).GetForCustomerAsync(customerId, booking.CarBookingId);
        Assert.Equal(preview.Final.Total, final.Final!.Total);
        Assert.Equal(250, final.Trip!.ActualKm);
        Assert.Equal(180, final.Estimate.Km);                         // estimate never overwritten
        Assert.Equal(booking.Estimate.Total, final.Estimate.Total);
        Assert.Equal(preview.BalanceDue, final.RemainingAmount);
        Assert.Contains(final.Timeline, t => t.Title.StartsWith("Final fare"));

        var collected = await trips.MarkBalanceCollectedAsync(driverId, booking.CarBookingId);
        Assert.Equal(CarPaymentStatus.BalanceCollected, collected.Booking.PaymentStatus);
        await Assert.ThrowsAsync<ConflictException>(() => trips.MarkBalanceCollectedAsync(driverId, booking.CarBookingId));

        var earnings = await trips.GetEarningsAsync(driverId);
        Assert.Equal(1, earnings.CompletedTrips);
        Assert.Equal(250, earnings.TotalKm);
        Assert.Equal(preview.Final.Total, earnings.TotalFare);
        Assert.Equal(preview.BalanceDue, earnings.BalanceCollected);
        Assert.Equal(0m, earnings.BalancePending);
    }

    [Fact]
    public async Task Start_trip_guards()
    {
        await using var db = TestDb.CreateContext();
        var (booking, _, driverId, carId) = await StartableBookingAsync(db);
        var (_, otherDriverId) = await _data.ListedCarAsync(db);
        var trips = CarTestData.Trips(db);

        await Assert.ThrowsAsync<NotFoundException>(() => trips.StartTripAsync(otherDriverId, booking.CarBookingId, new StartTripRequest(100, null, null)));
        await Assert.ThrowsAsync<ValidationAppException>(() => trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(-5, null, null)));
        await Assert.ThrowsAsync<ValidationAppException>(() => trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(100, 95m, 85m)));

        await trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(1000, null, null));
        var again = await Assert.ThrowsAsync<ConflictException>(() => trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(1000, null, null)));
        Assert.Equal("This trip has already started.", again.Message);

        // A started trip can no longer be cancelled by anyone.
        await Assert.ThrowsAsync<ConflictException>(() => CarTestData.Bookings(db).CancelByDriverAsync(driverId, booking.CarBookingId, "x"));
        await trips.CompleteTripAsync(driverId, booking.CarBookingId, new EndTripRequest(1300, 0, 0m, null, null, null));

        // The next trip in the same car can't start below where the last one ended (1,300 km).
        var customer = await _data.CustomerAsync(db);
        var next = await CarTestData.PaidBookingAsync(db, customer, carId, new FakeRazorpayService(), new TimeOnly(18, 0));
        var nextPickup = DateTime.UtcNow.AddMinutes(20);
        await db.CarBookings.Where(b => b.CarBookingId == next.CarBookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.PickupAt, nextPickup).SetProperty(b => b.EndsAt, nextPickup.AddHours(4)));
        db.ChangeTracker.Clear();
        var view = await trips.GetForDriverAsync(driverId, next.CarBookingId);
        Assert.Equal(1300, view.LastEndOdometerKm);
        var lower = await Assert.ThrowsAsync<ValidationAppException>(() => trips.StartTripAsync(driverId, next.CarBookingId, new StartTripRequest(1200, null, null)));
        Assert.Contains("1,300", lower.Errors.Single());
    }

    [Fact]
    public async Task Cannot_start_hours_before_pickup()
    {
        await using var db = TestDb.CreateContext();
        var (carId, driverId) = await _data.ListedCarAsync(db);
        var customer = await _data.CustomerAsync(db);
        var booking = await CarTestData.PaidBookingAsync(db, customer, carId, new FakeRazorpayService());   // 5 days away

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            CarTestData.Trips(db).StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(100, null, null)));
        Assert.StartsWith("You can start this trip from", error.Message);
    }

    [Fact]
    public async Task End_trip_rejects_bad_readings_uncapped_night_halts_and_unexplained_extras()
    {
        await using var db = TestDb.CreateContext();
        var (booking, _, driverId, _) = await StartableBookingAsync(db);
        var trips = CarTestData.Trips(db);
        await trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(24520, null, null));

        var lowerEnd = await Assert.ThrowsAsync<ValidationAppException>(() =>
            trips.PreviewEndAsync(driverId, booking.CarBookingId, new EndTripRequest(24000, 0, 0m, null, null, null)));
        Assert.Contains("End KM can't be less than start KM (24,520).", lowerEnd.Errors);

        // Started just now: no midnight crossed, so no night halt can be claimed.
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            trips.PreviewEndAsync(driverId, booking.CarBookingId, new EndTripRequest(24600, 1, 0m, null, null, null)));
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            trips.PreviewEndAsync(driverId, booking.CarBookingId, new EndTripRequest(24600, 0, 200m, " ", null, null)));
        await Assert.ThrowsAsync<ValidationAppException>(() =>
            trips.PreviewEndAsync(driverId, booking.CarBookingId, new EndTripRequest(24520 + 20000, 0, 0m, null, null, null)));

        // Short trip: only the km actually driven, plus the short-distance base fare.
        var shortTrip = await trips.PreviewEndAsync(driverId, booking.CarBookingId, new EndTripRequest(24560, 0, 0m, null, null, null));
        Assert.Equal(40, shortTrip.ActualKm);
        Assert.Equal(500m + 40 * 15m, shortTrip.Final.Total);

        await trips.CompleteTripAsync(driverId, booking.CarBookingId, new EndTripRequest(24560, 0, 0m, null, null, null));
        var done = await Assert.ThrowsAsync<ConflictException>(() =>
            trips.CompleteTripAsync(driverId, booking.CarBookingId, new EndTripRequest(99999, 0, 0m, null, null, null)));
        Assert.Equal("This trip is already completed.", done.Message);
    }

    [Fact]
    public async Task Admin_correction_recalculates_the_fare_and_is_audited_with_a_reason()
    {
        await using var db = TestDb.CreateContext();
        var (booking, customerId, driverId, _) = await StartableBookingAsync(db);
        var trips = CarTestData.Trips(db);
        await trips.StartTripAsync(driverId, booking.CarBookingId, new StartTripRequest(24520, null, null));
        await trips.CompleteTripAsync(driverId, booking.CarBookingId, new EndTripRequest(25520, 0, 0m, null, null, null));   // typo: 1,000 km

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            trips.AdminCorrectFareAsync(1, booking.CarBookingId, new AdminCorrectFareRequest(24520, 24720, 0, 0m, null, "")));
        var corrected = await trips.AdminCorrectFareAsync(1, booking.CarBookingId,
            new AdminCorrectFareRequest(24520, 24720, 0, 0m, null, "Driver mistyped the end reading; photo of odometer shows 24,720."));

        Assert.Equal(200, corrected.Booking.Trip!.ActualKm);
        Assert.Equal(300m + 200 * 15m, corrected.Booking.Final!.Total);
        var audit = Assert.Single(corrected.History, h => h.Action == "FinalFareCorrected");
        Assert.Equal("Admin", audit.ActorRole);
        Assert.Contains("1,000 km", audit.OldValue);
        Assert.Contains("200 km", audit.NewValue);

        var customerView = await CarTestData.Bookings(db).GetForCustomerAsync(customerId, booking.CarBookingId);
        Assert.Equal(corrected.Booking.Final.Total, customerView.Final!.Total);
        Assert.Contains(customerView.Timeline, t => t.Title.StartsWith("Final fare corrected"));
    }

    [Fact]
    public async Task Drivers_never_see_unpaid_bookings()
    {
        await using var db = TestDb.CreateContext();
        var (carId, driverId) = await _data.ListedCarAsync(db);
        var customer = await _data.CustomerAsync(db);
        var unpaid = await CarTestData.Bookings(db).CreateAsync(customer, CarTestData.Request(carId));
        var trips = CarTestData.Trips(db);

        Assert.Empty(await trips.ListForDriverAsync(driverId, DriverBookingScope.Upcoming));
        await Assert.ThrowsAsync<NotFoundException>(() => trips.GetForDriverAsync(driverId, unpaid.CarBookingId));
        await Assert.ThrowsAsync<NotFoundException>(() => trips.StartTripAsync(driverId, unpaid.CarBookingId, new StartTripRequest(100, null, null)));
    }

    public void Dispose()
    {
        _data.Dispose();
        GC.SuppressFinalize(this);
    }
}
