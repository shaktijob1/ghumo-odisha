using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>
/// Listed cars, customers and paid car bookings for the Cars tests, created straight in the real
/// database and removed again on Dispose. Each instance uses its own city so searches don't collide.
/// </summary>
public sealed class CarTestData : IDisposable
{
    private readonly List<int> _driverIds = [];
    private readonly List<int> _customerIds = [];

    public string City { get; } = "Testcity" + Random.Shared.Next(100000, 999999);
    public static CarRentalOptions Options_ { get; } = new();
    public static DateOnly PickupDate { get; } = DateOnly.FromDateTime(CarRentalCalendar.UtcToIndia(DateTime.UtcNow).AddDays(5));
    public static TimeOnly TenAm { get; } = new(10, 0);

    public static CarBookingService Bookings(GhumoOdishaDbContext db, FakeRazorpayService? razorpay = null) =>
        new(db, razorpay ?? new FakeRazorpayService(), Options.Create(Options_), NullLogger<CarBookingService>.Instance);

    public static CarBookingPaymentService Payments(GhumoOdishaDbContext db, FakeRazorpayService razorpay) =>
        new(db, razorpay, Bookings(db, razorpay), Options.Create(new RazorpayOptions { KeyId = "rzp_test" }), Options.Create(Options_),
            NullLogger<CarBookingPaymentService>.Instance);

    public static CarCatalogService Catalog(GhumoOdishaDbContext db) => new(db, Options.Create(Options_));

    public static CarTripService Trips(GhumoOdishaDbContext db) => new(db, Bookings(db), Options.Create(Options_));

    /// <summary>An approved driver with an approved, priced car: ₹15/km, base ₹500 up to 100 km then ₹300, night ₹400.</summary>
    public async Task<(int CarId, int DriverId)> ListedCarAsync(GhumoOdishaDbContext db, CarStatus carStatus = CarStatus.Approved, DriverStatus driverStatus = DriverStatus.Approved)
    {
        var now = DateTime.UtcNow;
        var driver = new Driver { Name = "Suresh Kumar", PhoneNumber = TestDb.RandomPhoneNumber(), Status = driverStatus, CreatedAt = now, UpdatedAt = now };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        _driverIds.Add(driver.DriverId);

        var car = new Car
        {
            DriverId = driver.DriverId, Brand = "Toyota", ModelName = "Innova Crysta", RegistrationNumber = $"OD02T{Random.Shared.Next(100000, 999999)}",
            FuelType = FuelType.Diesel, SeatCapacity = 7, HasAc = true, BaseCity = City, Status = carStatus, CreatedAt = now, UpdatedAt = now,
            Photos = [new CarPhoto { Kind = CarPhotoKind.Exterior, ImageUrl = "/uploads/cars/x.jpg", CreatedAt = now }]
        };
        db.Cars.Add(car);
        await db.SaveChangesAsync();

        var pricing = new CarPricing
        {
            CarId = car.CarId, PricePerKm = 15m, NightHaltPrice = 400m, Status = CarPricingStatus.Approved,
            SubmittedByRole = "Admin", SubmittedById = 1, SubmittedAt = now,
            Tiers = [new CarPricingTier { UpToKm = 100, BaseFare = 500m }, new CarPricingTier { UpToKm = null, BaseFare = 300m }]
        };
        db.CarPricings.Add(pricing);
        await db.SaveChangesAsync();
        car.ActivePricingId = pricing.CarPricingId;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (car.CarId, driver.DriverId);
    }

    public async Task<int> CustomerAsync(GhumoOdishaDbContext db)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer { Name = "Car Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        _customerIds.Add(customer.CustomerId);
        return customer.CustomerId;
    }

    public static CreateCarBookingRequest Request(int carId, TimeOnly? time = null, int hours = 12, int km = 180, DateOnly? date = null) =>
        new(carId, date ?? PickupDate, time ?? TenAm, hours, km, "Hotel Mayfair, Bhubaneswar", null, Guid.NewGuid());

    public static VerifyPaymentRequest Verify(string orderId) => new(orderId, "pay_" + Guid.NewGuid().ToString("N")[..14], "sig");

    public static async Task<CarBookingDto> PaidBookingAsync(GhumoOdishaDbContext db, int customerId, int carId, FakeRazorpayService razorpay,
        TimeOnly? time = null, int hours = 12, int km = 180)
    {
        var booking = await Bookings(db, razorpay).CreateAsync(customerId, Request(carId, time, hours, km));
        var order = await Payments(db, razorpay).CreateOrderAsync(customerId, booking.CarBookingId);
        return await Payments(db, razorpay).VerifyAsync(customerId, booking.CarBookingId, Verify(order.OrderId));
    }

    public void Dispose()
    {
        using var db = TestDb.CreateContext();
        var carIds = db.Cars.Where(c => _driverIds.Contains(c.DriverId)).Select(c => c.CarId).ToList();
        var bookingIds = db.CarBookings.Where(b => carIds.Contains(b.CarId)).Select(b => b.CarBookingId).ToList();
        db.CarAuditEvents.Where(e => (e.CarBookingId != null && bookingIds.Contains(e.CarBookingId.Value))
            || (e.EntityType != CarAuditEntity.Driver && carIds.Contains(e.EntityId))).ExecuteDelete();
        db.CarTripExecutions.Where(e => bookingIds.Contains(e.CarBookingId)).ExecuteDelete();
        db.CarBookings.Where(b => bookingIds.Contains(b.CarBookingId)).ExecuteDelete();
        db.Cars.Where(c => carIds.Contains(c.CarId)).ExecuteUpdate(s => s.SetProperty(c => c.ActivePricingId, (int?)null));
        db.Cars.Where(c => carIds.Contains(c.CarId)).ExecuteDelete();
        db.Drivers.Where(d => _driverIds.Contains(d.DriverId)).ExecuteDelete();
        db.Customers.Where(c => _customerIds.Contains(c.CustomerId)).ExecuteDelete();
    }
}
