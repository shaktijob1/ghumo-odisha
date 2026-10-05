using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Collections;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>24-hour expiry of unpaid requests, deleting a date whose bookings were never confirmed,
/// and the admin Collections sheet.</summary>
public class RequestExpiryAndCollectionsTests
{
    private static BookingService CreateService(GhumoOdishaDbContext db) =>
        new(db, new FakeRazorpayService(), new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db, int totalSeats = 10)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Expiry Trip {Guid.NewGuid():N}", Description = "Automated test trip.", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
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
        var customer = new Customer { Name = "Expiry Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    private static async Task<int> RequestAsync(BookingService service, Trip trip, TripDateSlot slot, Customer customer, int seats = 2)
    {
        var r = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, seats, null, AgreedToTerms: true));
        return r.Booking.BookingId;
    }

    private static Task BackdateAsync(GhumoOdishaDbContext db, int bookingId, TimeSpan age) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Bookings SET RequestedAt = {DateTime.UtcNow - age}, UpdatedAt = {DateTime.UtcNow - age} WHERE BookingId = {bookingId}");

    // ---------- 24-hour expiry ----------

    [Fact]
    public async Task UnpaidRequest_OlderThan24Hours_IsCancelled_AndSeatsUntouched()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await RequestAsync(service, trip, slot, customer);
        await BackdateAsync(db, bookingId, TimeSpan.FromHours(25));

        await service.ExpireUnpaidRequestsAsync();

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(BookingStatus.Cancelled, booking.BookingStatus);
        Assert.NotNull(booking.CancelledAt);
        Assert.Equal(10, await db.TripDateSlots.AsNoTracking().Where(s => s.TripDateSlotId == slot.TripDateSlotId).Select(s => s.AvailableSeats).SingleAsync());
        Assert.True(await db.BookingEvents.AnyAsync(e => e.BookingId == bookingId && e.Title == "Request expired" && e.IsVisibleToCustomer));
    }

    [Fact]
    public async Task UnpaidRequest_YoungerThan24Hours_IsLeftAlone()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await RequestAsync(service, trip, slot, customer);
        await BackdateAsync(db, bookingId, TimeSpan.FromHours(23));

        await service.ExpireUnpaidRequestsAsync();

        Assert.Equal(BookingStatus.AwaitingPayment, await db.Bookings.AsNoTracking().Where(b => b.BookingId == bookingId).Select(b => b.BookingStatus).SingleAsync());
    }

    [Fact]
    public async Task OldRequest_WithRazorpayOrderJustCreated_IsNotExpiredMidPayment()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await RequestAsync(service, trip, slot, customer);
        await BackdateAsync(db, bookingId, TimeSpan.FromHours(30));
        // The customer opened the payment screen a minute ago.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Bookings SET RazorpayOrderId = 'order_test', UpdatedAt = {DateTime.UtcNow.AddMinutes(-1)} WHERE BookingId = {bookingId}");

        await service.ExpireUnpaidRequestsAsync();

        Assert.Equal(BookingStatus.AwaitingPayment, await db.Bookings.AsNoTracking().Where(b => b.BookingId == bookingId).Select(b => b.BookingStatus).SingleAsync());
    }

    [Fact]
    public async Task ManualAdminBooking_IsCreatedConfirmed_WithSeatsDeductedAndPaymentRecorded()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);

        var bookingId = await service.CreateManualBookingAsync(new CreateManualBookingRequest(
            customer.CustomerId, null, null, null, trip.TripId, slot.TripDateSlotId, 2, 500m, PaymentMethod.Upi, "UPI-123", BookingSource.Phone, null));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(BookingStatus.Confirmed, booking.BookingStatus);
        Assert.NotNull(booking.ConfirmedAt);
        Assert.Equal(2000m, booking.TotalAmount);
        Assert.Equal(500m, booking.AdvanceAmount);
        Assert.Equal(1500m, booking.RemainingAmount);
        Assert.Equal(8, await db.TripDateSlots.AsNoTracking().Where(s => s.TripDateSlotId == slot.TripDateSlotId).Select(s => s.AvailableSeats).SingleAsync());
        var payment = await db.BookingPayments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId);
        Assert.Equal(500m, payment.Amount);
        Assert.Equal(PaymentMethod.Upi, payment.Method);
        Assert.Equal("UPI-123", payment.Reference);
    }

    [Fact]
    public async Task ManualAdminBooking_WithMorePaidThanTotal_CreatesNothing()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);

        await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateManualBookingAsync(new CreateManualBookingRequest(
            customer.CustomerId, null, null, null, trip.TripId, slot.TripDateSlotId, 1, 5000m, PaymentMethod.Cash, null, BookingSource.Phone, null)));

        Assert.False(await db.Bookings.AnyAsync(b => b.TripId == trip.TripId));
        Assert.Equal(10, await db.TripDateSlots.AsNoTracking().Where(s => s.TripDateSlotId == slot.TripDateSlotId).Select(s => s.AvailableSeats).SingleAsync());
    }

    [Fact]
    public async Task UnpaidCheckout_IsHiddenFromCustomer_ListedForAdminAsRequested_UntilExpiryCancelsIt()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await RequestAsync(service, trip, slot, customer);

        var mine = await service.GetCustomerBookingsAsync(customer.CustomerId, 1, 20);
        Assert.Empty(mine.Items);

        // Admin sees it as Requested, and it holds no seats.
        var listed = Assert.Single(await service.GetBookingsForTripAsync(trip.TripId));
        Assert.Equal(BookingStatus.AwaitingPayment, listed.BookingStatus);
        Assert.Single(await service.GetBookingsForDateSlotAsync(slot.TripDateSlotId));
        Assert.Equal(10, await db.TripDateSlots.AsNoTracking().Where(s => s.TripDateSlotId == slot.TripDateSlotId).Select(s => s.AvailableSeats).SingleAsync());

        // Once the unpaid-request expiry cancels it, it drops out of the admin lists again.
        await db.Bookings.Where(b => b.BookingId == bookingId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.RequestedAt, DateTime.UtcNow.AddHours(-(BookingService.UnpaidRequestExpiryHours + 1))));
        await service.ExpireUnpaidRequestsAsync();
        Assert.Empty(await service.GetBookingsForTripAsync(trip.TripId));
    }

    // ---------- Deleting a date ----------

    [Fact]
    public async Task DeleteDateSlot_WithOnlyRejectedAndExpiredBookings_Succeeds_AndKeepsTheirDates()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var rejectedId = await RequestAsync(service, trip, slot, customer);
        await service.RejectBookingAsync(rejectedId, new RejectBookingRequest("Full"));

        var other = new Customer { Name = "Other", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Customers.Add(other);
        await db.SaveChangesAsync();
        var expiredId = await RequestAsync(service, trip, slot, other);
        await BackdateAsync(db, expiredId, TimeSpan.FromHours(25));
        await service.ExpireUnpaidRequestsAsync();

        await new TripService(db, null!).DeleteDateSlotAsync(slot.TripDateSlotId);

        Assert.False(await db.TripDateSlots.AnyAsync(s => s.TripDateSlotId == slot.TripDateSlotId));
        var rejected = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == rejectedId);
        Assert.Null(rejected.TripDateSlotId);
        Assert.Equal(slot.StartDate, rejected.RemovedSlotStartDate);
        Assert.Equal(slot.EndDate, rejected.RemovedSlotEndDate);

        // The customer's history still shows the dates.
        var dto = await service.GetCustomerBookingDetailAsync(customer.CustomerId, rejectedId);
        Assert.Equal(slot.StartDate, dto.StartDate);
        Assert.Null(dto.TripDateSlotId);
    }

    [Fact]
    public async Task DeleteDateSlot_WithConfirmedBooking_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await RequestAsync(service, trip, slot, customer);
        await service.ConfirmBookingAsync(bookingId, new ConfirmBookingRequest(500m));

        await Assert.ThrowsAsync<ConflictException>(() => new TripService(db, null!).DeleteDateSlotAsync(slot.TripDateSlotId));
    }

    [Fact]
    public async Task DeleteDateSlot_WithUnpaidCheckout_Succeeds_AndClosesTheCheckout()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(CreateService(db), trip, slot, customer);

        await new TripService(db, null!).DeleteDateSlotAsync(slot.TripDateSlotId);

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Null(booking.TripDateSlotId);
        // Closed, so a payment that still arrives for it is refunded rather than confirmed.
        Assert.Equal(BookingStatus.Cancelled, booking.BookingStatus);
    }

    // ---------- Collections ----------

    [Fact]
    public async Task CollectionSheet_ListsOnlyConfirmedBookings_WithPaidAndRemainingTotals()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);

        var confirmedId = await RequestAsync(service, trip, slot, customer, seats: 3);           // total 3000
        await service.ConfirmBookingAsync(confirmedId, new ConfirmBookingRequest(1000m));

        var other = new Customer { Name = "Second", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Customers.Add(other);
        await db.SaveChangesAsync();
        await RequestAsync(service, trip, slot, other, seats: 2);                                // still Requested — not listed

        var collections = new CollectionService(db, null!);
        var sheet = await collections.GetSheetAsync(trip.TripId, slot.TripDateSlotId);

        var item = Assert.Single(sheet.Items);
        Assert.Equal(confirmedId, item.BookingId);
        Assert.Equal(3000m, sheet.Totals.TotalAmount);
        Assert.Equal(1000m, sheet.Totals.Paid);
        Assert.Equal(2000m, sheet.Totals.Remaining);

        // The coordinator collects the balance in cash on the trip.
        await service.AddPaymentAsync(confirmedId, new AddBookingPaymentRequest(2000m, PaymentMethod.Cash, null, null, null));
        db.ChangeTracker.Clear();
        sheet = await collections.GetSheetAsync(trip.TripId, null);
        Assert.Equal(3000m, sheet.Totals.Paid);
        Assert.Equal(0m, sheet.Totals.Remaining);
        Assert.Equal(PaymentStatus.Paid, sheet.Items[0].PaymentStatus);

        var trips = await collections.GetTripsAsync();
        var listed = Assert.Single(trips, t => t.TripId == trip.TripId);
        Assert.Equal(slot.TripDateSlotId, Assert.Single(listed.Departures).TripDateSlotId);
    }
}
