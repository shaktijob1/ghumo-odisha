using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Coupons.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

public class CouponServiceTests
{
    private static BookingService CreateBookingService(GhumoOdishaDbContext db) =>
        new(
            db,
            new FakeRazorpayService(),
            new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Title = $"Test Trip {Guid.NewGuid():N}",
            Description = "A trip created for automated tests.",
            AmountPerPerson = 1000m,
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = DateOnly.FromDateTime(now.AddDays(30)),
            EndDate = DateOnly.FromDateTime(now.AddDays(32)),
            TotalSeats = 20,
            AvailableSeats = 20,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);

        var customer = new Customer
        {
            Name = "Coupon Test Customer",
            PhoneNumber = TestDb.RandomPhoneNumber(),
            IsVerified = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    private static string RandomCode() => $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    [Fact]
    public async Task Redeem_FirstBooking_IsNewCustomer_AndEarnsCommissionPerSeat()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var bookings = CreateBookingService(db);
        var coupons = new CouponService(db);

        var couponId = await coupons.CreateAsync(new AdminCreateCouponRequest(RandomCode(), "Rahul Das", 100m, null, null));
        var booking = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 3, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(booking.Booking.BookingId, new ConfirmBookingRequest(0m));
        await coupons.RedeemAsync(couponId, customer.CustomerId, booking.Booking.BookingId, 100m);

        var rows = await coupons.GetBookingsAsync(couponId);
        var row = Assert.Single(rows);
        Assert.True(row.IsNewCustomer);
        Assert.Equal(3, row.NumberOfSeats);
        Assert.Equal(600m, row.CommissionAmount);
        Assert.Equal(600m, row.CommissionEarned);

        var summary = (await coupons.GetAllAsync()).Single(c => c.CouponCodeId == couponId);
        Assert.Equal("Rahul Das", summary.HolderName);
        Assert.Equal(1, summary.NewCustomerCount);
        Assert.Equal(0, summary.ExistingCustomerCount);
        Assert.Equal(600m, summary.CommissionEarned);
    }

    [Fact]
    public async Task Redeem_AfterAnEarlierConfirmedBooking_IsExistingCustomer()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var bookings = CreateBookingService(db);
        var coupons = new CouponService(db);

        var earlier = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(earlier.Booking.BookingId, new ConfirmBookingRequest(0m));

        var couponId = await coupons.CreateAsync(new AdminCreateCouponRequest(RandomCode(), "Priya Nayak", 100m, null, null, CommissionPerSeat: 250m));
        var booking = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(booking.Booking.BookingId, new ConfirmBookingRequest(0m));
        await coupons.RedeemAsync(couponId, customer.CustomerId, booking.Booking.BookingId, 100m);

        var row = Assert.Single(await coupons.GetBookingsAsync(couponId));
        Assert.False(row.IsNewCustomer);
        Assert.Equal(500m, row.CommissionAmount);
    }

    [Fact]
    public async Task FirstTimeOnlyCoupon_AcceptsCustomerWhoseOnlyBookingIsStillRequested()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var bookings = CreateBookingService(db);
        var coupons = new CouponService(db);

        var code = RandomCode();
        await coupons.CreateAsync(new AdminCreateCouponRequest(code, "Holder", 100m, null, null, IsFirstTimeCustomerOnly: true));
        var booking = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true));

        var validation = await coupons.ValidateAsync(code, customer.CustomerId);
        Assert.Equal(100m, validation.DiscountAmount);

        await bookings.ConfirmBookingAsync(booking.Booking.BookingId, new ConfirmBookingRequest(0m));
        await Assert.ThrowsAsync<ConflictException>(() => coupons.ValidateAsync(code, customer.CustomerId));
    }

    [Fact]
    public async Task CancelledBooking_EarnsNoCommission()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var bookings = CreateBookingService(db);
        var coupons = new CouponService(db);

        var couponId = await coupons.CreateAsync(new AdminCreateCouponRequest(RandomCode(), "Holder", 100m, null, null));
        var booking = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(booking.Booking.BookingId, new ConfirmBookingRequest(0m));
        await coupons.RedeemAsync(couponId, customer.CustomerId, booking.Booking.BookingId, 100m);

        // Through the real cancel path — setting the status directly left rows with no cancelled
        // date and no timeline step, which then showed up in the admin as a mismatched booking.
        await bookings.CancelBookingAsync(booking.Booking.BookingId, new CancelBookingRequest(null));

        var row = Assert.Single(await coupons.GetBookingsAsync(couponId));
        Assert.Equal(400m, row.CommissionAmount);
        Assert.Equal(0m, row.CommissionEarned);
        Assert.Equal(0m, (await coupons.GetAllAsync()).Single(c => c.CouponCodeId == couponId).CommissionEarned);
    }
}
