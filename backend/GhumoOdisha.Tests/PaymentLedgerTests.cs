using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Company;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Payments.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Invoices;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Booking payments: the confirm advance, later offline instalments, and the invoice built from them.</summary>
public class PaymentLedgerTests
{
    private static BookingService CreateService(GhumoOdishaDbContext db, FakeRazorpayService? razorpay = null) =>
        new(db, razorpay ?? new FakeRazorpayService(), new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db, int totalSeats = 10)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip
        {
            Title = $"Ledger Trip {Guid.NewGuid():N}",
            Description = "A trip created for automated tests.",
            AmountPerPerson = 1000m,
            IncludesStay = true,
            Status = TripStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = DateOnly.FromDateTime(now.AddDays(20)),
            EndDate = DateOnly.FromDateTime(now.AddDays(22)),
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats,
            Status = TripDateSlotStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);

        var customer = new Customer { Name = "Ledger Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    private static async Task<int> CreateConfirmedBookingAsync(BookingService service, Trip trip, TripDateSlot slot, Customer customer, int seats, decimal advance,
        PaymentMethod method = PaymentMethod.Cash)
    {
        var request = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, seats, null, AgreedToTerms: true));
        await service.ConfirmBookingAsync(request.Booking.BookingId, new ConfirmBookingRequest(advance, Method: method, PaymentReference: "RCPT-1"));
        return request.Booking.BookingId;
    }

    [Fact]
    public async Task Confirm_WithOfflineAdvance_RecordsItAsTheFirstPayment()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);

        var bookingId = await CreateConfirmedBookingAsync(service, trip, slot, customer, seats: 2, advance: 500m, PaymentMethod.Upi);

        var payment = await db.BookingPayments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId);
        Assert.Equal(500m, payment.Amount);
        Assert.Equal(PaymentMethod.Upi, payment.Method);
        Assert.Equal("RCPT-1", payment.Reference);
        Assert.Equal(PaymentRecordedBy.Admin, payment.RecordedBy);
    }

    [Fact]
    public async Task Confirm_WithZeroAdvance_RecordsNoPayment()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);

        var bookingId = await CreateConfirmedBookingAsync(service, trip, slot, customer, seats: 1, advance: 0m);

        Assert.False(await db.BookingPayments.AnyAsync(p => p.BookingId == bookingId));
    }

    [Fact]
    public async Task AddPayment_ReducesBalance_AndMarksPaidWhenCleared()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await CreateConfirmedBookingAsync(service, trip, slot, customer, seats: 2, advance: 198m);

        await service.AddPaymentAsync(bookingId, new AddBookingPaymentRequest(802m, PaymentMethod.Cash, null, "Collected at pickup"));
        var partial = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(1000m, partial.AdvanceAmount);
        Assert.Equal(1000m, partial.RemainingAmount);
        Assert.Equal(PaymentStatus.AdvancePaid, partial.PaymentStatus);

        await service.AddPaymentAsync(bookingId, new AddBookingPaymentRequest(1000m, PaymentMethod.BankTransfer, "UTR123", null));
        var paid = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(2000m, paid.AdvanceAmount);
        Assert.Equal(0m, paid.RemainingAmount);
        Assert.Equal(PaymentStatus.Paid, paid.PaymentStatus);
        Assert.Equal(3, await db.BookingPayments.CountAsync(p => p.BookingId == bookingId));
    }

    [Fact]
    public async Task AddPayment_MoreThanBalance_IsRejected_AndNothingChanges()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await CreateConfirmedBookingAsync(service, trip, slot, customer, seats: 1, advance: 99m);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.AddPaymentAsync(bookingId, new AddBookingPaymentRequest(902m, PaymentMethod.Cash, null, null)));

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(99m, booking.AdvanceAmount);
        Assert.Equal(1, await db.BookingPayments.CountAsync(p => p.BookingId == bookingId));
    }

    [Fact]
    public async Task AddPayment_OnUnconfirmedBooking_IsRejected()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var request = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true));

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddPaymentAsync(request.Booking.BookingId, new AddBookingPaymentRequest(100m, PaymentMethod.Cash, null, null)));
    }

    [Fact]
    public async Task RemovePayment_OfflinePayment_RestoresBalance()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await CreateConfirmedBookingAsync(service, trip, slot, customer, seats: 1, advance: 99m);
        await service.AddPaymentAsync(bookingId, new AddBookingPaymentRequest(901m, PaymentMethod.Cash, null, null));
        var cash = await db.BookingPayments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId && p.Amount == 901m);

        await service.RemovePaymentAsync(bookingId, cash.BookingPaymentId);

        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(99m, booking.AdvanceAmount);
        Assert.Equal(901m, booking.RemainingAmount);
        Assert.Equal(PaymentStatus.AdvancePaid, booking.PaymentStatus);
    }

    [Fact]
    public async Task OnlinePayment_IsRecordedAsRazorpay_AndCannotBeRemoved()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var razorpay = new FakeRazorpayService();
        var service = CreateService(db, razorpay);
        var payments = new BookingPaymentService(db, razorpay, service, new CouponService(db),
            Options.Create(new RazorpayOptions { KeyId = "rzp_test_fake" }), NullLogger<BookingPaymentService>.Instance);

        var request = await service.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        var order = await payments.CreateOrderAsync(customer.CustomerId, request.Booking.BookingId, BookingPaymentPlan.Partial, null);
        await payments.VerifyAndConfirmAsync(customer.CustomerId, request.Booking.BookingId, new VerifyPaymentRequest(order.OrderId, "pay_online_1", "sig"));

        var online = await db.BookingPayments.AsNoTracking().SingleAsync(p => p.BookingId == request.Booking.BookingId);
        Assert.Equal(PaymentMethod.Razorpay, online.Method);
        Assert.Equal(198m, online.Amount);
        Assert.Equal("pay_online_1", online.Reference);
        Assert.Equal(PaymentRecordedBy.Customer, online.RecordedBy);

        await Assert.ThrowsAsync<ConflictException>(() => service.RemovePaymentAsync(request.Booking.BookingId, online.BookingPaymentId));
    }

    [Fact]
    public async Task Invoice_RendersForConfirmedBooking_AndIsRefusedForOtherCustomers()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        await using var db = TestDb.CreateContext();
        var (trip, slot, customer) = await SeedAsync(db);
        var service = CreateService(db);
        var bookingId = await CreateConfirmedBookingAsync(service, trip, slot, customer, seats: 5, advance: 495m);
        await service.AddPaymentAsync(bookingId, new AddBookingPaymentRequest(1000m, PaymentMethod.Upi, "UTR999", null));

        var invoices = new QuestPdfInvoiceService(db, Options.Create(new CompanyOptions { Name = "Ghumo Odisha", Phone = "+918093731041" }));

        var customerPdf = await invoices.GenerateInvoicePdfAsync(customer.CustomerId, bookingId);
        var adminPdf = await invoices.GenerateAdminInvoicePdfAsync(bookingId);
        Assert.True(customerPdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(adminPdf, 0, 4));

        await Assert.ThrowsAsync<NotFoundException>(() => invoices.GenerateInvoicePdfAsync(customer.CustomerId + 100000, bookingId));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(5, 2)]
    [InlineData(9, 4)]
    [InlineData(10, 5)]
    public void RoomAllocation_OneRoomPerTwoSeats_RoundedDown_MinimumOne(int seats, int rooms) =>
        Assert.Equal(rooms, RoomAllocation.ForSeats(seats));
}
