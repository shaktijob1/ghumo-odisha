using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

public class BookingServiceTests
{
    private static BookingService CreateService(GhumoOdisha.Infrastructure.Persistence.GhumoOdishaDbContext db, FakeRazorpayService? razorpay = null) =>
        new(
            db,
            razorpay ?? new FakeRazorpayService(),
            new FakeFast2SmsWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedTripSlotAndCustomerAsync(
        GhumoOdisha.Infrastructure.Persistence.GhumoOdishaDbContext db, int totalSeats = 10)
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
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);

        var customer = new Customer
        {
            Name = "Test Customer",
            PhoneNumber = TestDb.RandomPhoneNumber(),
            IsVerified = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);

        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    [Fact]
    public async Task RequestBooking_CreatesRequestedStatus_AndNeverTouchesAvailableSeats()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);

        var result = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));

        Assert.Equal(BookingStatus.Requested, result.Booking.BookingStatus);
        Assert.Equal(2000m, result.Booking.TotalAmount);

        var slotAfter = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(10, slotAfter.AvailableSeats);
    }

    [Fact]
    public async Task RequestBooking_IgnoresClientSuppliedPriceFields_RecomputesFromTripPrice()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);

        // CreateBookingRequest has no price/amount field at all — this asserts the server-computed
        // total matches Trip.AmountPerPerson x seats regardless of anything a client could send.
        var result = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 3, null));

        Assert.Equal(trip.AmountPerPerson * 3, result.Booking.TotalAmount);
    }

    [Fact]
    public async Task RequestBooking_SameClientRequestIdTwice_DoesNotCreateADuplicate()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);
        var clientRequestId = Guid.NewGuid();

        var first = await service.RequestBookingAsync(customer.CustomerId,
            new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, clientRequestId));

        var second = await service.RequestBookingAsync(customer.CustomerId,
            new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, clientRequestId));

        Assert.Equal(first.Booking.BookingId, second.Booking.BookingId);

        var count = await db.Bookings.CountAsync(b => b.CustomerId == customer.CustomerId && b.ClientRequestId == clientRequestId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ConfirmBooking_DeductsSeats_OnlyOnConfirm()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 5);
        var service = CreateService(db);

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));

        var slotBeforeConfirm = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(5, slotBeforeConfirm.AvailableSeats);

        await service.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(500m));

        // ConfirmBookingAsync deducts seats via a raw SQL UPDATE that bypasses the change tracker
        // by design (see BookingService) — a real request gets a fresh, per-request DbContext, so
        // this staleness only shows up here because the test reuses one context across every step.
        db.ChangeTracker.Clear();

        var slotAfterConfirm = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(3, slotAfterConfirm.AvailableSeats);

        var booking = await db.Bookings.SingleAsync(b => b.BookingId == requested.Booking.BookingId);
        Assert.Equal(BookingStatus.Confirmed, booking.BookingStatus);
        Assert.Equal(PaymentStatus.AdvancePaid, booking.PaymentStatus);
    }

    [Fact]
    public async Task ConfirmBooking_InsufficientSeats_ThrowsAndLeavesBookingRequested()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 3);
        var service = CreateService(db);

        var bookingA = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));
        var bookingB = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));

        await service.ConfirmBookingAsync(bookingA.Booking.BookingId, new ConfirmBookingRequest(0m));

        await Assert.ThrowsAsync<GhumoOdisha.Application.Exceptions.InsufficientSeatsException>(
            () => service.ConfirmBookingAsync(bookingB.Booking.BookingId, new ConfirmBookingRequest(0m)));

        var bookingBAfter = await db.Bookings.SingleAsync(b => b.BookingId == bookingB.Booking.BookingId);
        Assert.Equal(BookingStatus.Requested, bookingBAfter.BookingStatus);
    }

    [Fact]
    public async Task AdminCancelBooking_ForOnlinePaidBooking_RefundsViaRazorpay_AndRestoresSeats()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 5);
        var razorpay = new FakeRazorpayService();
        var service = CreateService(db, razorpay);

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));
        await service.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(2000m, 0, "pay_test_123"));
        db.ChangeTracker.Clear();

        await service.CancelBookingAsync(requested.Booking.BookingId, new CancelBookingRequest(null));
        db.ChangeTracker.Clear();

        Assert.Equal(1, razorpay.RefundCallCount);
        Assert.Equal("pay_test_123", razorpay.LastRefundedPaymentId);

        var booking = await db.Bookings.SingleAsync(b => b.BookingId == requested.Booking.BookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.BookingStatus);
        Assert.Equal(PaymentStatus.Refunded, booking.PaymentStatus);
        Assert.Contains("Refunded in full via Razorpay", booking.AdminNotes);

        var slotAfter = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(5, slotAfter.AvailableSeats);
    }

    [Fact]
    public async Task AdminCancelBooking_WhenRazorpayRefundFails_BlocksCancellation_AndLeavesBookingConfirmed()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 5);
        var razorpay = new FakeRazorpayService { ShouldFailRefund = true };
        var service = CreateService(db, razorpay);

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));
        await service.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(2000m, 0, "pay_test_456"));
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CancelBookingAsync(requested.Booking.BookingId, new CancelBookingRequest(null)));

        var booking = await db.Bookings.SingleAsync(b => b.BookingId == requested.Booking.BookingId);
        Assert.Equal(BookingStatus.Confirmed, booking.BookingStatus);

        var slotAfter = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(3, slotAfter.AvailableSeats);
    }

    [Fact]
    public async Task AdminCancelBooking_ForOfflinePaidBooking_SkipsRazorpayRefund_ButStillCancels()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 5);
        var razorpay = new FakeRazorpayService();
        var service = CreateService(db, razorpay);

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null));
        // No RazorpayPaymentId — mirrors a cash advance collected offline and recorded by admin.
        await service.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(500m));
        db.ChangeTracker.Clear();

        await service.CancelBookingAsync(requested.Booking.BookingId, new CancelBookingRequest("Trip called off due to weather."));
        db.ChangeTracker.Clear();

        Assert.Equal(0, razorpay.RefundCallCount);

        var booking = await db.Bookings.SingleAsync(b => b.BookingId == requested.Booking.BookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.BookingStatus);
        Assert.Equal(PaymentStatus.Refunded, booking.PaymentStatus);
        Assert.Equal("Trip called off due to weather.", booking.AdminNotes);
    }
}
