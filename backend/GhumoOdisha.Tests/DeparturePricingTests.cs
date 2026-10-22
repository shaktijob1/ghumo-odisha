using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>
/// Per-departure price and early-booking price: worked out on the server from stored data for the
/// website request, the payment order, the confirm and the admin's manual booking.
/// </summary>
public class DeparturePricingTests
{
    private static (BookingService Bookings, BookingPaymentService Payments) CreateServices(GhumoOdishaDbContext db)
    {
        var razorpay = new FakeRazorpayService();
        var bookings = new BookingService(db, razorpay, new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }), NullLogger<BookingService>.Instance);
        var payments = new BookingPaymentService(db, razorpay, bookings, new CouponService(db),
            Options.Create(new RazorpayOptions { KeyId = "rzp_test_fake" }), NullLogger<BookingPaymentService>.Instance);
        return (bookings, payments);
    }

    /// <summary>Trip at ₹6,000; departure at ₹5,999 with an optional ₹5,499 early price ending on <paramref name="earlyBirdEndsOn"/>.</summary>
    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db, DateOnly? earlyBirdEndsOn)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Pricing Trip {Guid.NewGuid():N}", Description = "Automated test trip.", AmountPerPerson = 6000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = TripCalendar.Today().AddDays(40),
            EndDate = TripCalendar.Today().AddDays(42),
            TotalSeats = 16,
            AvailableSeats = 16,
            Status = TripDateSlotStatus.Active,
            PricePerPerson = 5999m,
            EarlyBirdPrice = earlyBirdEndsOn is null ? null : 5499m,
            EarlyBirdEndsOn = earlyBirdEndsOn,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        var customer = new Customer { Name = "Pricing Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    [Fact]
    public void SlotPricing_UsesDeparturePrice_ThenEarlyPriceUpToAndIncludingItsLastDay()
    {
        var trip = new Trip { AmountPerPerson = 6000m };
        var today = new DateOnly(2026, 10, 15);

        Assert.Equal(6000m, SlotPricing.PricePerPerson(trip, null, today));
        Assert.Equal(6000m, SlotPricing.PricePerPerson(trip, new TripDateSlot(), today));
        Assert.Equal(5999m, SlotPricing.PricePerPerson(trip, new TripDateSlot { PricePerPerson = 5999m }, today));

        var early = new TripDateSlot { PricePerPerson = 5999m, EarlyBirdPrice = 5499m, EarlyBirdEndsOn = today };
        Assert.Equal(5499m, SlotPricing.PricePerPerson(trip, early, today.AddDays(-30)));
        Assert.Equal(5499m, SlotPricing.PricePerPerson(trip, early, today));
        Assert.Equal(5999m, SlotPricing.PricePerPerson(trip, early, today.AddDays(1)));
    }

    [Fact]
    public async Task WebsiteBooking_DuringEarlyBooking_IsChargedTheEarlyPrice()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, TripCalendar.Today().AddDays(5));
        var (bookings, payments) = CreateServices(db);

        var request = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        var stored = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == request.Booking.BookingId);
        Assert.Equal(5499m, stored.AmountPerPerson);
        Assert.Equal(10998m, stored.TotalAmount);

        // Paying in full charges the early total less the full-payment offer.
        var full = await payments.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Full, null);
        Assert.Equal(10998m - full.DiscountApplied, full.AmountPaise / 100m);
    }

    [Fact]
    public async Task WebsiteBooking_AfterEarlyBookingEnds_IsChargedTheDeparturePrice()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, TripCalendar.Today().AddDays(-1));
        var (bookings, payments) = CreateServices(db);

        var request = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        var full = await payments.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Full, null);

        Assert.Equal(11998m - full.DiscountApplied, full.AmountPaise / 100m);
    }

    [Fact]
    public async Task CheckoutStartedOnTheLastEarlyDay_IsStillChargedAndConfirmedAtTheEarlyPrice()
    {
        await using var db = TestDb.CreateContext();
        // Early booking ended yesterday; the customer started checkout two days ago, inside it.
        var (trip, slot, customer) = await SeedAsync(db, TripCalendar.Today().AddDays(-1));
        var (bookings, payments) = CreateServices(db);
        var request = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true));
        var bookingId = request.Booking.BookingId;
        var twoDaysAgo = DateTime.UtcNow.AddDays(-2);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Bookings SET RequestedAt = {twoDaysAgo} WHERE BookingId = {bookingId}");
        // Forget the tracked copy (still holding today's RequestedAt), as a new request would.
        db.ChangeTracker.Clear();

        var full = await payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Full, null);
        Assert.Equal(5499m - full.DiscountApplied, full.AmountPaise / 100m);

        await bookings.ConfirmBookingAsync(bookingId, new ConfirmBookingRequest(500m, 0m, null, PaymentMethod.Cash, null));
        var confirmed = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(5499m, confirmed.AmountPerPerson);
        Assert.Equal(5499m, confirmed.TotalAmount);
        Assert.Equal(4999m, confirmed.RemainingAmount);
    }

    [Fact]
    public async Task TripPage_ShowsTheSamePriceCheckoutCharges()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, _) = await SeedAsync(db, TripCalendar.Today().AddDays(5));
        // A second departure without its own price, at the trip's ₹6,000.
        db.TripDateSlots.Add(new TripDateSlot
        {
            TripId = trip.TripId, StartDate = TripCalendar.Today().AddDays(70), EndDate = TripCalendar.Today().AddDays(72),
            TotalSeats = 10, AvailableSeats = 10, Status = TripDateSlotStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var detail = await new Application.Trips.TripService(db, null!).GetTripDetailAsync(trip.TripId);

        var early = detail.DateSlots.Single(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(5499m, early.PricePerPerson);
        Assert.Equal(5999m, early.RegularPrice);
        Assert.Equal(TripCalendar.Today().AddDays(5), early.EarlyBirdEndsOn);
        var plain = detail.DateSlots.Single(s => s.TripDateSlotId != slot.TripDateSlotId);
        Assert.Equal(6000m, plain.PricePerPerson);
        Assert.Null(plain.EarlyBirdEndsOn);
        Assert.Equal(5499m, detail.FromPrice);
        Assert.Equal(6000m, detail.AmountPerPerson);
    }

    [Fact]
    public async Task ManualAdminBooking_UsesTheEarlyPrice_AndRejectsPayingMoreThanIt()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, TripCalendar.Today().AddDays(5));
        var (bookings, _) = CreateServices(db);

        // ₹5,499 × 2 = ₹10,998: paying the ₹11,998 regular total is refused, nothing is created.
        await Assert.ThrowsAsync<ValidationAppException>(() => bookings.CreateManualBookingAsync(new CreateManualBookingRequest(
            customer.CustomerId, null, null, null, trip.TripId, slot.TripDateSlotId, 2, 11998m, PaymentMethod.Upi, null, BookingSource.Phone, null)));
        Assert.Equal(16, await db.TripDateSlots.AsNoTracking().Where(s => s.TripDateSlotId == slot.TripDateSlotId).Select(s => s.AvailableSeats).SingleAsync());

        var bookingId = await bookings.CreateManualBookingAsync(new CreateManualBookingRequest(
            customer.CustomerId, null, null, null, trip.TripId, slot.TripDateSlotId, 2, 10998m, PaymentMethod.Upi, null, BookingSource.Phone, null));
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(5499m, booking.AmountPerPerson);
        Assert.Equal(10998m, booking.TotalAmount);
        Assert.Equal(0m, booking.RemainingAmount);
    }
}
