using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
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
/// Every way onto a date slot — request, manual booking, payment order, payment verify — must
/// refuse departed, closed and full departures, whatever a (possibly tampered) client sends.
/// </summary>
public class SlotSafetyTests
{
    private static BookingService CreateBookingService(GhumoOdishaDbContext db, FakeRazorpayService razorpay) =>
        new(db, razorpay, new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static BookingPaymentService CreatePaymentService(GhumoOdishaDbContext db, FakeRazorpayService razorpay, BookingService bookingService) =>
        new(db, razorpay, bookingService, new CouponService(db),
            Options.Create(new RazorpayOptions { KeyId = "rzp_test_fake" }),
            NullLogger<BookingPaymentService>.Instance);

    private static async Task<(Trip Trip, TripDateSlot Slot)> SeedTripAndSlotAsync(
        GhumoOdishaDbContext db, int totalSeats = 10, int? availableSeats = null, int startInDays = 30,
        TripDateSlotStatus status = TripDateSlotStatus.Active)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Title = $"Slot Safety Trip {Guid.NewGuid():N}",
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
            StartDate = DateOnly.FromDateTime(now.AddDays(startInDays)),
            EndDate = DateOnly.FromDateTime(now.AddDays(startInDays + 2)),
            TotalSeats = totalSeats,
            AvailableSeats = availableSeats ?? totalSeats,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        await db.SaveChangesAsync();
        return (trip, slot);
    }

    private static async Task<Customer> SeedCustomerAsync(GhumoOdishaDbContext db)
    {
        var customer = new Customer
        {
            Name = "Slot Safety Customer",
            PhoneNumber = TestDb.RandomPhoneNumber(),
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task RequestBooking_ForDepartedSlot_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, startInDays: -2);
        var customer = await SeedCustomerAsync(db);
        var service = CreateBookingService(db, new FakeRazorpayService());

        await Assert.ThrowsAsync<DepartureClosedException>(() =>
            service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true)));
    }

    [Fact]
    public async Task RequestBooking_ForInactiveSlot_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, status: TripDateSlotStatus.Inactive);
        var customer = await SeedCustomerAsync(db);
        var service = CreateBookingService(db, new FakeRazorpayService());

        await Assert.ThrowsAsync<DepartureClosedException>(() =>
            service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true)));
    }

    [Fact]
    public async Task RequestBooking_ForFullSlot_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, totalSeats: 5, availableSeats: 0);
        var customer = await SeedCustomerAsync(db);
        var service = CreateBookingService(db, new FakeRazorpayService());

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true)));
        Assert.Contains("fully booked", ex.Message);
    }

    [Fact]
    public async Task ManualBooking_MoreSeatsThanAvailable_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, totalSeats: 10, availableSeats: 2);
        var customer = await SeedCustomerAsync(db);
        var service = CreateBookingService(db, new FakeRazorpayService());

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateManualBookingAsync(new CreateManualBookingRequest(
            customer.CustomerId, null, null, null, trip.TripId, slot.TripDateSlotId, 3, 0m,
            BookingSource.Phone, BookingStatus.Requested, null)));
    }

    [Fact]
    public async Task ManualBooking_ForDepartedSlot_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, startInDays: -1);
        var customer = await SeedCustomerAsync(db);
        var service = CreateBookingService(db, new FakeRazorpayService());

        await Assert.ThrowsAsync<DepartureClosedException>(() => service.CreateManualBookingAsync(new CreateManualBookingRequest(
            customer.CustomerId, null, null, null, trip.TripId, slot.TripDateSlotId, 1, 0m,
            BookingSource.Phone, BookingStatus.Requested, null)));
    }

    [Fact]
    public async Task CreatePaymentOrder_ChargesExactly99PerSeat_ComputedServerSide()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db);
        var customer = await SeedCustomerAsync(db);
        var razorpay = new FakeRazorpayService();
        var bookingService = CreateBookingService(db, razorpay);
        var paymentService = CreatePaymentService(db, razorpay, bookingService);

        var request = await bookingService.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 4, null, AgreedToTerms: true));
        var order = await paymentService.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Partial, null);

        Assert.Equal(99L * 4 * 100, order.AmountPaise);
    }

    [Fact]
    public async Task CreatePaymentOrder_WhenSlotFilledAfterRequest_IsRejected_BeforeAnyOrderExists()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, totalSeats: 3);
        var customer = await SeedCustomerAsync(db);
        var razorpay = new FakeRazorpayService();
        var bookingService = CreateBookingService(db, razorpay);
        var paymentService = CreatePaymentService(db, razorpay, bookingService);

        var request = await bookingService.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE TripDateSlots SET AvailableSeats = 1 WHERE TripDateSlotId = {slot.TripDateSlotId}");

        await Assert.ThrowsAsync<ConflictException>(() =>
            paymentService.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Partial, null));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == request.Booking.BookingId);
        Assert.Null(booking.RazorpayOrderId);
    }

    [Fact]
    public async Task VerifyPayment_WhenSeatsTakenWhilePaying_RefundsInFull_AndLeavesBookingRequested()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, totalSeats: 3);
        var payer = await SeedCustomerAsync(db);
        var other = await SeedCustomerAsync(db);
        var razorpay = new FakeRazorpayService();
        var bookingService = CreateBookingService(db, razorpay);
        var paymentService = CreatePaymentService(db, razorpay, bookingService);

        var payerBooking = await bookingService.RequestBookingAsync(payer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 3, null, AgreedToTerms: true));
        var order = await paymentService.CreateOrderAsync(payer.CustomerId, payerBooking.Booking.BookingId, BookingPaymentPlan.Partial, null);

        // While the payer is in the Razorpay checkout, the admin confirms someone else onto every seat.
        var otherBooking = await bookingService.RequestBookingAsync(other.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 3, null, AgreedToTerms: true));
        await bookingService.ConfirmBookingAsync(otherBooking.Booking.BookingId, new ConfirmBookingRequest(0m));

        await Assert.ThrowsAsync<ConflictException>(() => paymentService.VerifyAndConfirmAsync(
            payer.CustomerId, payerBooking.Booking.BookingId, new VerifyPaymentRequest(order.OrderId, "pay_fake_123", "sig")));

        Assert.Equal("pay_fake_123", razorpay.LastRefundedPaymentId);
        var after = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == payerBooking.Booking.BookingId);
        Assert.Equal(BookingStatus.Requested, after.BookingStatus);
        var slotAfter = await db.TripDateSlots.AsNoTracking().SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(0, slotAfter.AvailableSeats);
    }

    [Fact]
    public async Task ConfirmBooking_ForDepartedSlot_IsRejected_AndSeatsUntouched()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAndSlotAsync(db, totalSeats: 5);
        var customer = await SeedCustomerAsync(db);
        var service = CreateBookingService(db, new FakeRazorpayService());

        var request = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        var past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE TripDateSlots SET StartDate = {past} WHERE TripDateSlotId = {slot.TripDateSlotId}");

        await Assert.ThrowsAsync<DepartureClosedException>(() => service.ConfirmBookingAsync(request.Booking.BookingId, new ConfirmBookingRequest(0m)));

        var slotAfter = await db.TripDateSlots.AsNoTracking().SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(5, slotAfter.AvailableSeats);
    }
}
