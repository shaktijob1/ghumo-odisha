using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Invoices;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Admin booking tools: seat changes, travellers, male/female counts, cancel without refund, timeline.</summary>
public class BookingAdminToolsTests
{
    private static BookingService CreateService(GhumoOdishaDbContext db, FakeRazorpayService? razorpay = null) =>
        new(db, razorpay ?? new FakeRazorpayService(), new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db, int totalSeats = 10)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Tools Trip {Guid.NewGuid():N}", Description = "Automated test trip.", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = DateOnly.FromDateTime(now.AddDays(25)),
            EndDate = DateOnly.FromDateTime(now.AddDays(27)),
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        var customer = new Customer { Name = "Tools Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    private static async Task<int> ConfirmedBookingAsync(BookingService service, Trip trip, TripDateSlot slot, Customer customer, int seats, decimal advance,
        string? razorpayPaymentId = null)
    {
        var r = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, seats, null, AgreedToTerms: true));
        await service.ConfirmBookingAsync(r.Booking.BookingId, new ConfirmBookingRequest(advance, RazorpayPaymentId: razorpayPaymentId));
        return r.Booking.BookingId;
    }

    private static Task<int> AvailableAsync(GhumoOdishaDbContext db, TripDateSlot slot) =>
        db.TripDateSlots.AsNoTracking().Where(s => s.TripDateSlotId == slot.TripDateSlotId).Select(s => s.AvailableSeats).SingleAsync();

    // ---------- Seat changes ----------

    [Fact]
    public async Task ReduceSeats_OnConfirmedBooking_RestoresSlotSeats_AndNeverRefunds()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, totalSeats: 10);
        var razorpay = new FakeRazorpayService();
        var service = CreateService(db, razorpay);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 5, advance: 4500m);

        await service.ChangeSeatsAsync(bookingId, new ChangeSeatsRequest(4, "One traveller dropped out"));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(4, booking.NumberOfSeats);
        Assert.Equal(4000m, booking.TotalAmount);
        Assert.Equal(4500m, booking.AdvanceAmount);   // nothing given back
        Assert.Equal(0m, booking.RemainingAmount);    // balance floors at zero
        Assert.Equal(PaymentStatus.Paid, booking.PaymentStatus);
        Assert.Equal(6, await AvailableAsync(db, slot));
        Assert.Equal(0, razorpay.RefundCallCount);
    }

    [Fact]
    public async Task IncreaseSeats_OnConfirmedBooking_DeductsOnlyTheDifference()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, totalSeats: 10);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 2, advance: 198m);

        await service.ChangeSeatsAsync(bookingId, new ChangeSeatsRequest(5, "Friends joined"));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(5000m, booking.TotalAmount);
        Assert.Equal(4802m, booking.RemainingAmount);
        Assert.Equal(5, await AvailableAsync(db, slot));
    }

    [Fact]
    public async Task IncreaseSeats_BeyondAvailability_IsRejected_AndNothingChanges()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, totalSeats: 4);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 3, advance: 0m);

        await Assert.ThrowsAsync<InsufficientSeatsException>(() => service.ChangeSeatsAsync(bookingId, new ChangeSeatsRequest(5, "More friends")));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(3, booking.NumberOfSeats);
        Assert.Equal(1, await AvailableAsync(db, slot));
    }

    [Fact]
    public async Task ReduceSeats_WhenDroppedSeatHasTraveller_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 3, advance: 0m);
        await new BookingTravellerService(db).UpdateTravellersAsync(bookingId, new UpdateTravellersRequest([
            new TravellerInput(3, "Seat Three", Gender.Male, 30, null, null)]));

        await Assert.ThrowsAsync<ConflictException>(() => service.ChangeSeatsAsync(bookingId, new ChangeSeatsRequest(2, "Drop one")));
    }

    [Fact]
    public async Task ChangeSeats_RecalculatesCouponCommission_AtTheSnapshottedRate()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 4, advance: 0m);
        var coupon = new CouponCode { Code = $"T{Guid.NewGuid():N}"[..12].ToUpperInvariant(), HolderName = "Agent", DiscountAmount = 100m, CommissionPerSeat = 200m, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.CouponCodes.Add(coupon);
        await db.SaveChangesAsync();
        db.CouponRedemptions.Add(new CouponRedemption { CouponCodeId = coupon.CouponCodeId, CustomerId = customer.CustomerId, BookingId = bookingId, DiscountAmount = 100m, NumberOfSeats = 4, CommissionAmount = 800m, IsNewCustomer = true, RedeemedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await service.ChangeSeatsAsync(bookingId, new ChangeSeatsRequest(3, "One cancelled"));

        var redemption = await db.CouponRedemptions.AsNoTracking().SingleAsync(r => r.BookingId == bookingId);
        Assert.Equal(3, redemption.NumberOfSeats);
        Assert.Equal(600m, redemption.CommissionAmount);
    }

    // ---------- Travellers ----------

    [Fact]
    public async Task Travellers_WithPhone_AreLinked_AndSeeTheBookingViewOnly()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        await using var db = TestDb.CreateContext();
        var (trip, slot, owner) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, owner, seats: 3, advance: 297m);
        var newPhone = TestDb.RandomPhoneNumber();

        await new BookingTravellerService(db).UpdateTravellersAsync(bookingId, new UpdateTravellersRequest([
            new TravellerInput(1, "Owner Person", Gender.Male, 34, "1234", owner.PhoneNumber),
            new TravellerInput(2, "Friend Person", Gender.Female, 29, "5678", "+91 " + newPhone),
            new TravellerInput(3, "Child Person", Gender.Female, 8, null, null)]));

        // A silent, unverified account was created for the new phone — no PIN, no OTP.
        var friend = await db.Customers.AsNoTracking().SingleAsync(c => c.PhoneNumber == newPhone);
        Assert.False(friend.IsVerified);
        Assert.Null(friend.PinHash);
        Assert.Equal("Friend Person", friend.Name);

        var friendView = await service.GetCustomerBookingsAsync(friend.CustomerId, 1, 20);
        var shared = Assert.Single(friendView.Items);
        Assert.Equal(bookingId, shared.BookingId);
        Assert.False(shared.IsOwner);
        Assert.Empty(shared.Payments);   // the booker's payment details stay private
        var ownerView = await service.GetCustomerBookingDetailAsync(owner.CustomerId, bookingId);
        Assert.True(ownerView.IsOwner);
        Assert.Equal(297m, Assert.Single(ownerView.Payments).Amount);

        // View-only: the linked traveller can't cancel it or download its invoice.
        await Assert.ThrowsAsync<NotFoundException>(() => service.CancelOwnBookingAsync(friend.CustomerId, bookingId));
        var invoices = new QuestPdfInvoiceService(db, Options.Create(new CompanyOptions()));
        await Assert.ThrowsAsync<NotFoundException>(() => invoices.GenerateInvoicePdfAsync(friend.CustomerId, bookingId));

        // Every seat filled with a gender → counts follow the list.
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(1, booking.MaleCount);
        Assert.Equal(2, booking.FemaleCount);
    }

    [Fact]
    public async Task Travellers_FullAadhaarNumber_IsRejected_NeverStored()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 1, advance: 0m);

        await Assert.ThrowsAsync<ValidationAppException>(() => new BookingTravellerService(db).UpdateTravellersAsync(bookingId,
            new UpdateTravellersRequest([new TravellerInput(1, "Someone", Gender.Male, 40, "123412341234", null)])));

        Assert.False(await db.BookingTravellers.AnyAsync(t => t.BookingId == bookingId));
    }

    [Fact]
    public async Task Travellers_MoreThanSeats_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 1, advance: 0m);

        await Assert.ThrowsAsync<ValidationAppException>(() => new BookingTravellerService(db).UpdateTravellersAsync(bookingId,
            new UpdateTravellersRequest([new TravellerInput(1, "One", null, null, null, null), new TravellerInput(2, "Two", null, null, null, null)])));
    }

    [Fact]
    public async Task GenderCounts_AboveSeats_AreRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 3, advance: 0m);
        var travellers = new BookingTravellerService(db);

        await travellers.UpdateGenderCountsAsync(bookingId, new UpdateGenderCountsRequest(2, 1));
        await Assert.ThrowsAsync<ValidationAppException>(() => travellers.UpdateGenderCountsAsync(bookingId, new UpdateGenderCountsRequest(2, 2)));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(2, booking.MaleCount);
        Assert.Equal(1, booking.FemaleCount);
    }

    // ---------- Cancel without refund ----------

    [Fact]
    public async Task CancelWithoutRefund_KeepsPayment_SkipsRazorpay_AndRestoresSeats()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db, totalSeats: 6);
        var razorpay = new FakeRazorpayService();
        var service = CreateService(db, razorpay);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 2, advance: 198m, razorpayPaymentId: "pay_keep_1");

        await service.CancelBookingAsync(bookingId, new CancelBookingRequest(null, WaiveRefund: true, Reason: "No-show on departure day"));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.BookingStatus);
        Assert.True(booking.RefundWaived);
        Assert.Equal("No-show on departure day", booking.CancellationReason);
        Assert.Equal(PaymentStatus.AdvancePaid, booking.PaymentStatus);
        Assert.Equal(0, razorpay.RefundCallCount);
        Assert.Equal(6, await AvailableAsync(db, slot));

        // Status and timeline must always agree.
        Assert.NotNull(booking.CancelledAt);
        var last = (await service.GetAdminBookingDetailAsync(bookingId)).Timeline[^1];
        Assert.Equal(BookingEventType.Cancelled, last.EventType);
        Assert.Contains("No-show on departure day", last.Description);
    }

    [Fact]
    public async Task CancelWithoutRefund_RequiresAReason()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 1, advance: 0m);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CancelBookingAsync(bookingId, new CancelBookingRequest(null, WaiveRefund: true)));
        Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId)).BookingStatus);
    }

    // ---------- Timeline ----------

    [Fact]
    public async Task Timeline_RecordsEachStep_AndHidesInternalOnesFromTheCustomer()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await ConfirmedBookingAsync(service, trip, slot, customer, seats: 2, advance: 198m);
        await service.AddPaymentAsync(bookingId, new AddBookingPaymentRequest(500m, PaymentMethod.Cash, null, null));
        var cash = await db.BookingPayments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId && p.Amount == 500m);
        await service.RemovePaymentAsync(bookingId, cash.BookingPaymentId);
        await service.ChangeSeatsAsync(bookingId, new ChangeSeatsRequest(3, "Added one"));

        var admin = await service.GetAdminBookingDetailAsync(bookingId);
        Assert.Equal(
            [BookingEventType.Requested, BookingEventType.Confirmed, BookingEventType.PaymentReceived, BookingEventType.PaymentRemoved, BookingEventType.SeatsChanged],
            admin.Timeline.Select(e => e.EventType).ToArray());

        var mine = await service.GetCustomerBookingDetailAsync(customer.CustomerId, bookingId);
        Assert.DoesNotContain(mine.Timeline, e => e.EventType == BookingEventType.PaymentRemoved);
        Assert.Contains(mine.Timeline, e => e.EventType == BookingEventType.SeatsChanged && e.Title == "Seats changed from 2 to 3");
        Assert.Equal(1, admin.RoomsAllotted);
    }
}
