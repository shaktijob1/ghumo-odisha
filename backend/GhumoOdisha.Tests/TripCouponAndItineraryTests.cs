using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>The per-trip coupon switch, and itinerary days staying in Day 1, 2, 3… order.</summary>
public class TripCouponAndItineraryTests
{
    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db, bool allowCoupons)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Title = $"Coupon Switch Trip {Guid.NewGuid():N}",
            Description = "A trip created for automated tests.",
            AmountPerPerson = 1000m,
            AllowCoupons = allowCoupons,
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = DateOnly.FromDateTime(now.AddDays(25)),
            EndDate = DateOnly.FromDateTime(now.AddDays(26)),
            TotalSeats = 10,
            AvailableSeats = 10,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);

        var customer = new Customer { Name = "Coupon Switch Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);

        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    private static async Task<string> AddCouponAsync(GhumoOdishaDbContext db)
    {
        var now = DateTime.UtcNow;
        var code = $"S{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        db.CouponCodes.Add(new CouponCode { Code = code, HolderName = "Agent", DiscountAmount = 100m, CommissionPerSeat = 0m, IsActive = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        return code;
    }

    private static (BookingService Bookings, BookingPaymentService Payments) CreateServices(GhumoOdishaDbContext db)
    {
        var razorpay = new FakeRazorpayService();
        var bookings = new BookingService(db, razorpay, new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }), NullLogger<BookingService>.Instance);
        var payments = new BookingPaymentService(db, razorpay, bookings, new CouponService(db),
            Options.Create(new RazorpayOptions { KeyId = "rzp_test_fake" }), NullLogger<BookingPaymentService>.Instance);
        return (bookings, payments);
    }

    [Fact]
    public async Task Coupon_IsRefused_WhenTheTripHasCouponsSwitchedOff()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, allowCoupons: false);
        var code = await AddCouponAsync(db);
        var (bookings, payments) = CreateServices(db);

        var request = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        Assert.False(request.Booking.CouponsAllowed);

        var error = await Assert.ThrowsAsync<ConflictException>(() =>
            payments.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Partial, code));
        Assert.Contains("not applicable", error.Message);

        // Paying without a code still works.
        var order = await payments.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Partial, null);
        Assert.False(string.IsNullOrEmpty(order.OrderId));
    }

    [Fact]
    public async Task Coupon_IsAccepted_WhenTheTripAllowsCoupons()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, allowCoupons: true);
        var code = await AddCouponAsync(db);
        var (bookings, payments) = CreateServices(db);

        var request = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        Assert.True(request.Booking.CouponsAllowed);

        var order = await payments.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Partial, code);
        Assert.False(string.IsNullOrEmpty(order.OrderId));
    }

    [Fact]
    public async Task ItineraryDays_AreListedByDayNumber_NotByWhenTheyWereAdded()
    {
        await using var db = TestDb.CreateContext();
        var (trip, _, _) = await SeedAsync(db, allowCoupons: true);
        var service = new TripService(db, null!);

        foreach (var day in new[] { 3, 1, 4, 2 })
        {
            await service.AddItineraryDayAsync(trip.TripId, new AddItineraryDayRequest(day, $"Day {day} title", "Plan"));
        }

        var detail = await service.GetAdminTripDetailAsync(trip.TripId);
        Assert.Equal([1, 2, 3, 4], detail.ItineraryDays.Select(d => d.DayNumber));
    }

    [Fact]
    public async Task ItineraryDay_NumberMustBeUnique_OnAddAndOnEdit()
    {
        await using var db = TestDb.CreateContext();
        var (trip, _, _) = await SeedAsync(db, allowCoupons: true);
        var service = new TripService(db, null!);

        await service.AddItineraryDayAsync(trip.TripId, new AddItineraryDayRequest(1, "Arrive", "Plan"));
        var day2 = await service.AddItineraryDayAsync(trip.TripId, new AddItineraryDayRequest(2, "Explore", "Plan"));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddItineraryDayAsync(trip.TripId, new AddItineraryDayRequest(2, "Duplicate", "Plan")));
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateItineraryDayAsync(day2, new UpdateItineraryDayRequest(1, "Explore", "Plan", 0)));

        // Saving a day under its own number (e.g. only the title changed) is fine.
        await service.UpdateItineraryDayAsync(day2, new UpdateItineraryDayRequest(2, "Explore more", "Plan", 0));
        var detail = await service.GetAdminTripDetailAsync(trip.TripId);
        Assert.Equal("Explore more", detail.ItineraryDays.Single(d => d.DayNumber == 2).Title);
    }
}
