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

    private static BookingService CreateService(GhumoOdisha.Infrastructure.Persistence.GhumoOdishaDbContext db, FakeFast2SmsWhatsAppService whatsApp) =>
        new(
            db,
            new FakeRazorpayService(),
            whatsApp,
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

        var result = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));

        Assert.Equal(BookingStatus.Requested, result.Booking.BookingStatus);
        Assert.Equal(2000m, result.Booking.TotalAmount);

        var slotAfter = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(10, slotAfter.AvailableSeats);
    }

    [Fact]
    public async Task RequestBooking_WithoutAgreeingToTerms_Throws()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.RequestBookingAsync(
            customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null)));

        Assert.False(await db.Bookings.AnyAsync(b => b.TripId == trip.TripId));
    }

    [Fact]
    public async Task RequestBooking_RecordsTermsAcceptance_SnapshottingCurrentText()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);

        var result = await service.RequestBookingAsync(customer.CustomerId,
            new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));

        var acceptance = await db.TermsAcceptances.SingleAsync(t => t.BookingId == result.Booking.BookingId);
        Assert.Equal(customer.CustomerId, acceptance.CustomerId);
        Assert.Equal(GhumoOdisha.Application.Legal.TermsAndConditionsContent.Version, acceptance.Version);
        Assert.Equal(GhumoOdisha.Application.Legal.TermsAndConditionsContent.Text, acceptance.Text);
    }

    [Fact]
    public async Task RequestBooking_IgnoresClientSuppliedPriceFields_RecomputesFromTripPrice()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);

        // CreateBookingRequest has no price/amount field at all — this asserts the server-computed
        // total matches Trip.AmountPerPerson x seats regardless of anything a client could send.
        var result = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 3, null, AgreedToTerms: true));

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
            new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, clientRequestId, AgreedToTerms: true));

        var second = await service.RequestBookingAsync(customer.CustomerId,
            new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, clientRequestId, AgreedToTerms: true));

        Assert.Equal(first.Booking.BookingId, second.Booking.BookingId);

        var count = await db.Bookings.CountAsync(b => b.CustomerId == customer.CustomerId && b.ClientRequestId == clientRequestId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RequestBooking_SameCustomerSameSlot_CancelsPriorRequest()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var service = CreateService(db);

        var first = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        var second = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 4, null, AgreedToTerms: true));

        var firstAfter = await db.Bookings.SingleAsync(b => b.BookingId == first.Booking.BookingId);
        Assert.Equal(BookingStatus.Cancelled, firstAfter.BookingStatus);

        var secondAfter = await db.Bookings.SingleAsync(b => b.BookingId == second.Booking.BookingId);
        Assert.Equal(BookingStatus.Requested, secondAfter.BookingStatus);

        // Never touched by a mere request — only an admin CONFIRM deducts seats.
        var slotAfter = await db.TripDateSlots.SingleAsync(s => s.TripDateSlotId == slot.TripDateSlotId);
        Assert.Equal(10, slotAfter.AvailableSeats);
    }

    [Fact]
    public async Task RequestBooking_SameCustomerDifferentSlot_LeavesEarlierRequestAlone()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slotA, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);

        var now = DateTime.UtcNow;
        var slotB = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = DateOnly.FromDateTime(now.AddDays(60)),
            EndDate = DateOnly.FromDateTime(now.AddDays(62)),
            TotalSeats = 10,
            AvailableSeats = 10,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slotB);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var first = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slotA.TripDateSlotId, 2, null, AgreedToTerms: true));
        await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slotB.TripDateSlotId, 2, null, AgreedToTerms: true));

        var firstAfter = await db.Bookings.SingleAsync(b => b.BookingId == first.Booking.BookingId);
        Assert.Equal(BookingStatus.Requested, firstAfter.BookingStatus);
    }

    [Fact]
    public async Task ConfirmBooking_DeductsSeats_OnlyOnConfirm()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 5);
        var service = CreateService(db);

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));

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
        var (trip, slot, customerA) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 3);
        var service = CreateService(db);

        // Two different customers competing for the same slot — a same-customer repeat request
        // would instead supersede/cancel the earlier one (see RequestBooking_SameCustomerSameSlot_*).
        var customerB = new Customer
        {
            Name = "Test Customer B",
            PhoneNumber = TestDb.RandomPhoneNumber(),
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Customers.Add(customerB);
        await db.SaveChangesAsync();

        var bookingA = await service.RequestBookingAsync(customerA.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        var bookingB = await service.RequestBookingAsync(customerB.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));

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

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
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

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
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

        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
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

    [Fact]
    public async Task ConfirmBooking_SendsBookingConfirmedTemplate_WithLastPickupPointAndEarliestTime()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var now = DateTime.UtcNow;
        db.PickupPoints.AddRange(
            new PickupPoint { TripId = trip.TripId, Location = "Master Canteen", Time = "07:00", DisplayOrder = 0, CreatedAt = now },
            new PickupPoint { TripId = trip.TripId, Location = "Airport Square", Time = "06:00", DisplayOrder = 1, CreatedAt = now },
            new PickupPoint { TripId = trip.TripId, Location = "Rasulgarh", Time = "08:00", DisplayOrder = 2, CreatedAt = now });
        await db.SaveChangesAsync();

        var whatsApp = new FakeFast2SmsWhatsAppService { BookingConfirmedTemplateConfigured = true };
        var service = CreateService(db, whatsApp);
        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));

        await service.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(1500m));

        Assert.Equal(1, whatsApp.BookingConfirmedCallCount);
        var message = whatsApp.LastBookingConfirmed!;
        Assert.Equal("Test Customer", message.CustomerName);
        Assert.Equal(trip.Title, message.TripTitle);
        Assert.Equal(slot.StartDate.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), message.TravelDate);
        Assert.Equal("Test Customer", message.PassengerName);
        Assert.Equal("2", message.Seats);
        Assert.Equal("1,500", message.AmountPaid);
        Assert.Equal($"GO-{requested.Booking.BookingId}", message.BookingReference);
        Assert.Equal("Rasulgarh", message.PickupPoint);
        Assert.Equal("6:00 AM", message.ReportingTime);
        // The dedicated template replaces the old customer text; only the organizer's copy uses the shared one.
        Assert.Equal(1, whatsApp.TemplateCallCount);
        Assert.Equal("919000000000", whatsApp.LastPhoneNumber);
    }

    [Fact]
    public async Task ConfirmBooking_WithoutBookingConfirmedTemplate_FallsBackToSharedTemplate()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedTripSlotAndCustomerAsync(db, totalSeats: 10);
        var whatsApp = new FakeFast2SmsWhatsAppService { BookingConfirmedTemplateConfigured = false };
        var service = CreateService(db, whatsApp);
        var requested = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true));

        await service.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(0m));

        Assert.Equal(0, whatsApp.BookingConfirmedCallCount);
        Assert.Equal(2, whatsApp.TemplateCallCount); // customer fallback + organizer copy
    }
}
