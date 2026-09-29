using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

/// <summary>
/// Last-line-of-defence constraints in MySQL for the Cars tables — they hold even if a service bug
/// slipped past the application checks.
/// </summary>
[Collection("Cars")]
public class CarSchemaTests
{
    [Fact]
    public async Task Database_refuses_an_end_odometer_below_the_start()
    {
        await using var db = TestDb.CreateContext();
        var (booking, cleanup) = await SeedBookingAsync(db);
        try
        {
            db.CarTripExecutions.Add(new CarTripExecution
            {
                CarBookingId = booking.CarBookingId,
                DriverId = booking.DriverId,
                CarId = booking.CarId,
                StartedAt = DateTime.UtcNow,
                StartOdometerKm = 24520,
                EndOdometerKm = 24000,
                ActualKm = -520,
            });

            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            db.ChangeTracker.Clear();
            await cleanup();
        }
    }

    [Fact]
    public async Task Database_refuses_a_second_trip_execution_for_one_booking()
    {
        await using var db = TestDb.CreateContext();
        var (booking, cleanup) = await SeedBookingAsync(db);
        try
        {
            CarTripExecution Start() => new()
            {
                CarBookingId = booking.CarBookingId,
                DriverId = booking.DriverId,
                CarId = booking.CarId,
                StartedAt = DateTime.UtcNow,
                StartOdometerKm = 100,
            };

            db.CarTripExecutions.Add(Start());
            await db.SaveChangesAsync();

            // A fresh tracker, like a second request: otherwise EF itself swaps the one-to-one
            // dependent and the insert never reaches the unique index being tested.
            db.ChangeTracker.Clear();
            db.CarTripExecutions.Add(Start());
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            db.ChangeTracker.Clear();
            await cleanup();
        }
    }

    [Fact]
    public async Task Database_refuses_an_unsupported_seat_capacity()
    {
        await using var db = TestDb.CreateContext();
        var driver = NewDriver();
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        try
        {
            db.Cars.Add(NewCar(driver.DriverId, seats: 6));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.Drivers.Where(d => d.DriverId == driver.DriverId).ExecuteDeleteAsync();
        }
    }

    internal static Driver NewDriver() => new()
    {
        Name = "Test Driver",
        PhoneNumber = TestDb.RandomPhoneNumber(),
        Status = DriverStatus.Approved,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    internal static Car NewCar(int driverId, int seats = 5) => new()
    {
        DriverId = driverId,
        Brand = "Maruti",
        ModelName = "Dzire",
        RegistrationNumber = $"OD02T{Random.Shared.Next(100000, 999999)}",
        FuelType = FuelType.Diesel,
        SeatCapacity = seats,
        HasAc = true,
        BaseCity = "Bhubaneswar",
        Status = CarStatus.Approved,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static async Task<(CarBooking Booking, Func<Task> Cleanup)> SeedBookingAsync(GhumoOdisha.Infrastructure.Persistence.GhumoOdishaDbContext db)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer { Name = "Car Test", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        var driver = NewDriver();
        db.Customers.Add(customer);
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();

        var car = NewCar(driver.DriverId);
        db.Cars.Add(car);
        await db.SaveChangesAsync();

        var pricing = new CarPricing
        {
            CarId = car.CarId,

            PricePerKm = 13m,
            NightHaltPrice = 400m,
            Status = CarPricingStatus.Approved,
            SubmittedByRole = "Admin",
            SubmittedById = 1,
            SubmittedAt = now,
            Tiers = [new CarPricingTier { UpToKm = null, BaseFare = 300m }],
        };
        db.CarPricings.Add(pricing);
        await db.SaveChangesAsync();

        var booking = new CarBooking
        {
            BookingNumber = Random.Shared.Next(100_000_000, 999_999_999),
            CustomerId = customer.CustomerId,
            CarId = car.CarId,
            DriverId = driver.DriverId,
            CarPricingId = pricing.CarPricingId,
            PickupCity = "Bhubaneswar",
            PickupAt = now.AddDays(3),
            DurationHours = 12,
            EndsAt = now.AddDays(3).AddHours(12),
            EstimatedKm = 50,
            BookingAmount = 99m,
            Status = CarBookingStatus.Confirmed,
            PaymentStatus = CarPaymentStatus.BookingAmountPaid,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.CarBookings.Add(booking);
        await db.SaveChangesAsync();

        return (booking, async () =>
        {
            await db.CarTripExecutions.Where(e => e.CarBookingId == booking.CarBookingId).ExecuteDeleteAsync();
            await db.CarBookings.Where(b => b.CarBookingId == booking.CarBookingId).ExecuteDeleteAsync();
            await db.Cars.Where(c => c.CarId == car.CarId).ExecuteDeleteAsync();
            await db.Drivers.Where(d => d.DriverId == driver.DriverId).ExecuteDeleteAsync();
            await db.Customers.Where(c => c.CustomerId == customer.CustomerId).ExecuteDeleteAsync();
        });
    }
}
