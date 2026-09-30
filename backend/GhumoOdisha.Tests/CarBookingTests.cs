using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Exceptions;

using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;



namespace GhumoOdisha.Tests;

/// <summary>
/// Car booking (Cars phase 4): the no-double-booking guarantee, server-side fares, the ₹99 payment,
/// cancellation and the manual refund desk.
/// </summary>
[Collection("Cars")]
public class CarBookingTests : IDisposable
{
    private readonly CarTestData _data = new();
    private string _city => _data.City;
    private static DateOnly PickupDate => CarTestData.PickupDate;
    private static TimeOnly TenAm => CarTestData.TenAm;

    private static CarBookingService Bookings(GhumoOdishaDbContext db, FakeRazorpayService? razorpay = null) => CarTestData.Bookings(db, razorpay);
    private static CarBookingPaymentService Payments(GhumoOdishaDbContext db, FakeRazorpayService razorpay) => CarTestData.Payments(db, razorpay);
    private static CarCatalogService Catalog(GhumoOdishaDbContext db) => CarTestData.Catalog(db);
    private Task<(int CarId, int DriverId)> ListedCarAsync(GhumoOdishaDbContext db, CarStatus carStatus = CarStatus.Approved, DriverStatus driverStatus = DriverStatus.Approved) =>
        _data.ListedCarAsync(db, carStatus, driverStatus);
    private Task<int> CustomerAsync(GhumoOdishaDbContext db) => _data.CustomerAsync(db);
    private static CreateCarBookingRequest Request(int carId, TimeOnly? time = null, int hours = 12, int km = 180, DateOnly? date = null) =>
        CarTestData.Request(carId, time, hours, km, date);
    private static VerifyPaymentRequest Verify(string orderId) => CarTestData.Verify(orderId);
    private static Task<CarBookingDto> PaidBookingAsync(GhumoOdishaDbContext db, int customerId, int carId, FakeRazorpayService razorpay, TimeOnly? time = null) =>
        CarTestData.PaidBookingAsync(db, customerId, carId, razorpay, time);

    // ---------- tests ----------

    [Fact]
    public async Task Two_customers_racing_for_the_same_car_and_time_only_one_gets_it()
    {
        await using var setup = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(setup);
        var a = await CustomerAsync(setup);
        var b = await CustomerAsync(setup);

        async Task<bool> TryBook(int customerId)
        {
            await using var db = TestDb.CreateContext();
            try
            {
                await Bookings(db).CreateAsync(customerId, Request(carId));
                return true;
            }
            catch (ConflictException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryBook(a), TryBook(b));

        Assert.Single(results, r => r);
        Assert.Equal(1, await setup.CarBookings.CountAsync(x => x.CarId == carId && x.Status == CarBookingStatus.PendingPayment));
    }

    [Fact]
    public async Task Turnaround_buffer_blocks_back_to_back_bookings_and_expired_holds_free_the_car()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(db);
        var a = await CustomerAsync(db);
        var b = await CustomerAsync(db);
        var service = Bookings(db);

        var first = await service.CreateAsync(a, Request(carId, new TimeOnly(8, 0), hours: 4));        // 08:00–12:00

        // 12:30 is inside the 60-minute turnaround after 12:00.
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(b, Request(carId, new TimeOnly(12, 30), hours: 4)));
        // 13:00 is clear of it.
        await service.CreateAsync(b, Request(carId, new TimeOnly(13, 0), hours: 4));

        // Once the first customer's payment hold runs out, their slot is free again.
        await db.CarBookings.Where(x => x.CarBookingId == first.CarBookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HoldExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        var c = await CustomerAsync(db);
        await service.CreateAsync(c, Request(carId, new TimeOnly(8, 0), hours: 4));
    }

    [Fact]
    public async Task Unapproved_cars_never_appear_and_cannot_be_booked()
    {
        await using var db = TestDb.CreateContext();
        var (pendingCar, _) = await ListedCarAsync(db, carStatus: CarStatus.Pending);
        var (suspendedDriverCar, _) = await ListedCarAsync(db, driverStatus: DriverStatus.Suspended);
        var (listedCar, _) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);

        var results = await Catalog(db).SearchAsync(new CarSearchQuery(_city, null, null, null, null));

        Assert.Equal([listedCar], results.Cars.Select(c => c.CarId));
        Assert.Contains(_city, results.Locations);
        await Assert.ThrowsAsync<NotFoundException>(() => Catalog(db).GetAsync(pendingCar, new CarWindowQuery(null, null, null)));
        await Assert.ThrowsAsync<ConflictException>(() => Bookings(db).CreateAsync(customer, Request(pendingCar)));
        await Assert.ThrowsAsync<ConflictException>(() => Bookings(db).CreateAsync(customer, Request(suspendedDriverCar)));
    }

    [Fact]
    public async Task Fare_comes_only_from_the_approved_pricing_and_is_frozen_on_the_booking()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);

        // 24 h from 10:00 spans one night; 180 km → base ₹300 + 180 × 15 + 1 × 400.
        var quote = await Catalog(db).QuoteAsync(carId, new CarQuoteRequest(PickupDate, TenAm, 24, CarTestData.Pickup, CarTestData.DropForTotal(180)));
        Assert.Equal(300m + 2700m + 400m, quote.EstimatedTotal);
        Assert.Equal(99m, quote.BookingAmount);
        Assert.Equal(quote.EstimatedTotal - 99m, quote.RemainingAmount);

        var booking = await Bookings(db).CreateAsync(customer, Request(carId, hours: 24));
        Assert.Equal(quote.EstimatedTotal, booking.Estimate.Total);
        Assert.Equal(1, booking.Estimate.Nights);

        // No minimum km: a short trip pays its own km plus the (higher) short-distance base fare.
        var shortQuote = await Catalog(db).QuoteAsync(carId, new CarQuoteRequest(PickupDate.AddDays(3), TenAm, 12, CarTestData.Pickup, CarTestData.DropForTotal(20)));
        Assert.Equal(500m + 20 * 15m, shortQuote.EstimatedTotal);

        // A later price change doesn't touch the existing booking's estimate.
        await db.CarPricings.Where(p => p.CarId == carId).ExecuteUpdateAsync(s => s.SetProperty(p => p.PricePerKm, 99m));
        var again = await Bookings(db).GetForCustomerAsync(customer, booking.CarBookingId);
        Assert.Equal(booking.Estimate.Total, again.Estimate.Total);
    }

    [Fact]
    public async Task Paying_confirms_the_booking_assigns_the_driver_and_a_retried_verify_is_harmless()
    {
        await using var db = TestDb.CreateContext();
        var (carId, driverId) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);
        var razorpay = new FakeRazorpayService();

        var created = await Bookings(db, razorpay).CreateAsync(customer, Request(carId));
        Assert.Equal(CarBookingStatus.PendingPayment, created.Status);
        Assert.Null(created.Driver);            // driver contact only after confirmation
        Assert.Null(created.RegistrationNumber);
        Assert.True(created.CanPay);

        var order = await Payments(db, razorpay).CreateOrderAsync(customer, created.CarBookingId);
        Assert.Equal(9900, order.AmountPaise);
        var verify = Verify(order.OrderId);
        var confirmed = await Payments(db, razorpay).VerifyAsync(customer, created.CarBookingId, verify);

        Assert.Equal(CarBookingStatus.Confirmed, confirmed.Status);
        Assert.Equal(CarPaymentStatus.BookingAmountPaid, confirmed.PaymentStatus);
        Assert.Equal(driverId, confirmed.Driver!.DriverId);
        Assert.NotNull(confirmed.RegistrationNumber);
        Assert.Equal(confirmed.Estimate.Total - 99m, confirmed.RemainingAmount);
        Assert.Contains(confirmed.Timeline, t => t.Title.StartsWith("Driver assigned"));

        var retried = await Payments(db, razorpay).VerifyAsync(customer, created.CarBookingId, verify);
        Assert.Equal(CarBookingStatus.Confirmed, retried.Status);
    }

    [Fact]
    public async Task If_someone_else_confirms_the_car_while_a_customer_pays_that_payment_is_refunded()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(db);
        var slow = await CustomerAsync(db);
        var fast = await CustomerAsync(db);
        var razorpay = new FakeRazorpayService();

        var slowBooking = await Bookings(db, razorpay).CreateAsync(slow, Request(carId));
        var slowOrder = await Payments(db, razorpay).CreateOrderAsync(slow, slowBooking.CarBookingId);

        // The slow customer's hold lapses and someone else books and pays for the same time.
        await db.CarBookings.Where(x => x.CarBookingId == slowBooking.CarBookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HoldExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        await PaidBookingAsync(db, fast, carId, razorpay);

        var error = await Assert.ThrowsAsync<ConflictException>(() => Payments(db, razorpay).VerifyAsync(slow, slowBooking.CarBookingId, Verify(slowOrder.OrderId)));
        Assert.Contains("refunded", error.Message);
        Assert.Equal(1, razorpay.RefundCallCount);

        var after = await Bookings(db, razorpay).GetForCustomerAsync(slow, slowBooking.CarBookingId);
        Assert.Equal(CarBookingStatus.Cancelled, after.Status);
        Assert.Equal(CarPaymentStatus.Refunded, after.PaymentStatus);
        Assert.Equal(1, await db.CarBookings.CountAsync(x => x.CarId == carId && x.Status == CarBookingStatus.Confirmed));
    }

    [Fact]
    public async Task Cancelling_a_paid_booking_queues_a_refund_that_can_only_be_issued_once()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);
        var razorpay = new FakeRazorpayService();
        var booking = await PaidBookingAsync(db, customer, carId, razorpay);
        var service = Bookings(db, razorpay);

        var cancelled = await service.CancelByCustomerAsync(customer, booking.CarBookingId, "Plans changed");
        Assert.Equal(CarBookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(CarPaymentStatus.RefundPending, cancelled.PaymentStatus);
        Assert.Equal(0, razorpay.RefundCallCount);            // never refunded automatically on cancel
        await Assert.ThrowsAsync<ConflictException>(() => service.CancelByCustomerAsync(customer, booking.CarBookingId, null));

        // The admin refund queue lists it by payment status; other queues don't.
        var queue = await service.ListAsync(null, CarPaymentStatus.RefundPending, booking.Reference, 1, 20);
        Assert.Contains(queue.Items, b => b.CarBookingId == booking.CarBookingId);
        var refunded = await service.ListAsync(null, CarPaymentStatus.Refunded, booking.Reference, 1, 20);
        Assert.DoesNotContain(refunded.Items, b => b.CarBookingId == booking.CarBookingId);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.IssueRazorpayRefundAsync(1, booking.CarBookingId, 150m));
        var issued = await service.IssueRazorpayRefundAsync(1, booking.CarBookingId, 99m);
        Assert.Equal(CarPaymentStatus.RefundProcessing, issued.Booking.PaymentStatus);
        await Assert.ThrowsAsync<ConflictException>(() => service.IssueRazorpayRefundAsync(1, booking.CarBookingId, 99m));
        Assert.Equal(1, razorpay.RefundCallCount);

        var settled = await service.SettleRefundAsync(1, booking.CarBookingId);
        Assert.Equal(CarPaymentStatus.Refunded, settled.Booking.PaymentStatus);
        Assert.Contains(settled.Booking.Timeline, t => t.Title == "₹99 refunded");
    }

    [Fact]
    public async Task Drivers_can_cancel_only_their_own_bookings_and_admins_can_keep_the_booking_amount()
    {
        await using var db = TestDb.CreateContext();
        var (carId, driverId) = await ListedCarAsync(db);
        var (_, otherDriverId) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);
        var razorpay = new FakeRazorpayService();
        var service = Bookings(db, razorpay);

        var first = await PaidBookingAsync(db, customer, carId, razorpay);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelByDriverAsync(otherDriverId, first.CarBookingId, "Not mine"));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CancelByDriverAsync(driverId, first.CarBookingId, " "));
        var byDriver = await service.CancelByDriverAsync(driverId, first.CarBookingId, "Car breakdown");
        Assert.Equal("Driver", byDriver.CancelledBy);
        Assert.Equal(CarPaymentStatus.RefundPending, byDriver.PaymentStatus);

        var second = await PaidBookingAsync(db, customer, carId, razorpay, new TimeOnly(15, 0));
        var byAdmin = await service.CancelByAdminAsync(1, second.CarBookingId, new AdminCancelCarBookingRequest("No-show policy", WaiveRefund: true));
        Assert.Equal(CarPaymentStatus.BookingAmountPaid, byAdmin.Booking.PaymentStatus);
        Assert.Contains(byAdmin.History, h => h.Action == "RefundWaived");
    }

    [Fact]
    public async Task Search_marks_a_booked_car_unavailable_for_that_window_only()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);
        await PaidBookingAsync(db, customer, carId, new FakeRazorpayService());

        var clash = await Catalog(db).SearchAsync(new CarSearchQuery(_city, PickupDate, new TimeOnly(14, 0), 4, null));
        var nextDay = await Catalog(db).SearchAsync(new CarSearchQuery(_city, PickupDate.AddDays(1), TenAm, 12, 7));

        Assert.False(clash.Cars.Single(c => c.CarId == carId).IsAvailable);
        var free = nextDay.Cars.Single(c => c.CarId == carId);
        Assert.True(free.IsAvailable);
        Assert.Equal("7 Seater Car", free.Category);
        Assert.Equal(15m, free.PricePerKm);
        Assert.Equal(300m, free.BaseFareFrom);
        Assert.Equal(500m, free.BaseFareTo);
    }

    [Fact]
    public async Task Pickup_too_soon_or_too_far_ahead_is_refused_with_a_clear_message()
    {
        await using var db = TestDb.CreateContext();
        var (carId, _) = await ListedCarAsync(db);
        var customer = await CustomerAsync(db);
        var nowIndia = CarRentalCalendar.UtcToIndia(DateTime.UtcNow).AddMinutes(30);

        var tooSoon = await Assert.ThrowsAsync<ValidationAppException>(() => Bookings(db).CreateAsync(customer,
            Request(carId, TimeOnly.FromDateTime(nowIndia), date: DateOnly.FromDateTime(nowIndia))));
        Assert.Contains(tooSoon.Errors, e => e.Contains("at least 2 hours"));

        await Assert.ThrowsAsync<ValidationAppException>(() => Bookings(db).CreateAsync(customer, Request(carId, date: PickupDate.AddDays(400))));
    }

    public void Dispose()
    {
        _data.Dispose();
        GC.SuppressFinalize(this);
    }
}
