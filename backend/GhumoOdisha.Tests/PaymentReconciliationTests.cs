using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Notifications;
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
/// "Money taken but booking not confirmed": the customer pays on Razorpay but the browser never
/// reports it (page refreshed or closed). Razorpay is asked directly and the booking confirmed through
/// the normal Confirm path; money that can't be used is refunded; reminders go out at 15 and 30 minutes.
/// </summary>
public class PaymentReconciliationTests
{
    private sealed record Services(BookingService Bookings, BookingPaymentService Payments, FakeRazorpayService Razorpay, FakeWhatsAppService WhatsApp);

    private static Services Create(GhumoOdishaDbContext db)
    {
        var razorpay = new FakeRazorpayService();
        var whatsApp = new FakeWhatsAppService();
        var bookings = new BookingService(db, razorpay, whatsApp,
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }), NullLogger<BookingService>.Instance);
        var payments = new BookingPaymentService(db, razorpay, bookings, new CouponService(db),
            Options.Create(new RazorpayOptions { KeyId = "rzp_test_fake", KeySecret = "secret" }), NullLogger<BookingPaymentService>.Instance);
        return new Services(bookings, payments, razorpay, whatsApp);
    }

    private static async Task<(Trip Trip, TripDateSlot Slot, Customer Customer)> SeedAsync(GhumoOdishaDbContext db, int seats = 10)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Reconcile Trip {Guid.NewGuid():N}", Description = "Automated test trip.", AmountPerPerson = 2000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();
        var slot = new TripDateSlot
        {
            TripId = trip.TripId, StartDate = TripCalendar.Today().AddDays(20), EndDate = TripCalendar.Today().AddDays(22),
            TotalSeats = seats, AvailableSeats = seats, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        var customer = new Customer { Name = "Reconcile Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return (trip, slot, customer);
    }

    private static async Task<int> RequestAsync(Services s, Trip trip, TripDateSlot slot, Customer customer, int seats = 2) =>
        (await s.Bookings.RequestBookingAsync(customer.CustomerId,
            new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, seats, null, AgreedToTerms: true))).Booking.BookingId;

    private static Task<Booking> ReloadAsync(GhumoOdishaDbContext db, int bookingId) =>
        db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);

    private static Task<int> SeatsLeftAsync(GhumoOdishaDbContext db, TripDateSlot slot) =>
        db.TripDateSlots.AsNoTracking().Where(x => x.TripDateSlotId == slot.TripDateSlotId).Select(x => x.AvailableSeats).SingleAsync();

    [Fact]
    public async Task PaidOnRazorpay_ButPageRefreshed_IsConfirmedByTheReconciler()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var order = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        var paymentId = s.Razorpay.Pay(order.OrderId, order.AmountPaise);

        // No verify call — the customer refreshed. Seats untouched until Razorpay is asked.
        Assert.Equal(10, await SeatsLeftAsync(db, slot));
        var result = await s.Payments.ReconcileAsync(bookingId);

        Assert.Equal(PaymentCheckStatus.Confirmed, result.Status);
        var booking = await ReloadAsync(db, bookingId);
        Assert.Equal(BookingStatus.Confirmed, booking.BookingStatus);
        Assert.Equal(198m, booking.AdvanceAmount);
        Assert.Equal(paymentId, booking.RazorpayPaymentId);
        Assert.Equal(8, await SeatsLeftAsync(db, slot));
        Assert.Equal(paymentId, (await db.BookingPaymentOrders.AsNoTracking().SingleAsync(o => o.RazorpayOrderId == order.OrderId)).ConfirmedPaymentId);

        // The browser's late verify (same payment) now just succeeds; nothing is deducted twice.
        await s.Payments.VerifyAndConfirmAsync(customer.CustomerId, bookingId, new VerifyPaymentRequest(order.OrderId, paymentId, "sig"));
        Assert.Equal(8, await SeatsLeftAsync(db, slot));
        Assert.Equal(0, s.Razorpay.RefundCallCount);
    }

    [Fact]
    public async Task PaymentOnAnEarlierReplacedOrder_IsStillFound_AndConfirmedAtThatOrdersAmount()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var partial = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        // The customer switched to "pay in full" (a new order replaces the booking's pointer)...
        var full = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Full, null);
        Assert.NotEqual(partial.OrderId, full.OrderId);
        // ...but the ₹198 order is the one that got paid.
        s.Razorpay.Pay(partial.OrderId, partial.AmountPaise);

        await s.Payments.ReconcileOpenCheckoutsAsync();

        var booking = await ReloadAsync(db, bookingId);
        Assert.Equal(BookingStatus.Confirmed, booking.BookingStatus);
        Assert.Equal(198m, booking.AdvanceAmount);
    }

    [Fact]
    public async Task AuthorizedPayment_IsCaptured_ThenConfirmed()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var order = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        s.Razorpay.Pay(order.OrderId, order.AmountPaise, authorizedOnly: true);

        Assert.Equal(PaymentCheckStatus.Confirmed, (await s.Payments.ReconcileAsync(bookingId)).Status);
        Assert.Equal(1, s.Razorpay.CaptureCallCount);
    }

    [Fact]
    public async Task PaymentForADifferentAmount_IsNeverConfirmed()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var order = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        s.Razorpay.Pay(order.OrderId, order.AmountPaise - 100);

        Assert.Equal(PaymentCheckStatus.Pending, (await s.Payments.ReconcileAsync(bookingId)).Status);
        Assert.Equal(10, await SeatsLeftAsync(db, slot));
        Assert.Equal(0, s.Razorpay.RefundCallCount);
    }

    [Fact]
    public async Task ComingBackToPay_AfterAlreadyPaying_ConfirmsInsteadOfChargingAgain()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var order = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        s.Razorpay.Pay(order.OrderId, order.AmountPaise);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Full, null));
        Assert.Equal(BookingPaymentService.AlreadyPaidMessage, ex.Message);
        Assert.Equal(BookingStatus.Confirmed, (await ReloadAsync(db, bookingId)).BookingStatus);
    }

    [Fact]
    public async Task BookingTheSameDateAgainAfterPaying_ConfirmsTheEarlierPaidCheckout()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var order = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        s.Razorpay.Pay(order.OrderId, order.AmountPaise);

        // The controller asks this before RequestBookingAsync would replace the earlier checkout.
        Assert.Equal(bookingId, await s.Payments.ConfirmPaidCheckoutForSlotAsync(customer.CustomerId, slot.TripDateSlotId));
        Assert.Equal(BookingStatus.Confirmed, (await ReloadAsync(db, bookingId)).BookingStatus);

        // Nothing paid: nothing to confirm, the new request goes ahead as normal.
        var (trip2, slot2, customer2) = await SeedAsync(db);
        var unpaid = await RequestAsync(s, trip2, slot2, customer2);
        await s.Payments.CreateOrderAsync(customer2.CustomerId, unpaid, BookingPaymentPlan.Partial, null);
        Assert.Null(await s.Payments.ConfirmPaidCheckoutForSlotAsync(customer2.CustomerId, slot2.TripDateSlotId));
    }

    [Fact]
    public async Task PaymentArrivingAfterTheCheckoutExpired_IsRefundedOnce()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var order = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Bookings SET RequestedAt = {DateTime.UtcNow.AddMinutes(-90)}, UpdatedAt = {DateTime.UtcNow.AddMinutes(-90)} WHERE BookingId = {bookingId}");
        await s.Bookings.ExpireUnpaidRequestsAsync();
        Assert.Equal(BookingStatus.Cancelled, (await ReloadAsync(db, bookingId)).BookingStatus);

        // The Razorpay window was still open and the customer paid anyway.
        var paymentId = s.Razorpay.Pay(order.OrderId, order.AmountPaise);
        await s.Payments.ReconcileClosedCheckoutsAsync();
        Assert.Equal(1, s.Razorpay.RefundCallCount);
        Assert.Equal(paymentId, s.Razorpay.LastRefundedPaymentId);
        Assert.Equal(paymentId, (await db.BookingPaymentOrders.AsNoTracking().SingleAsync(o => o.RazorpayOrderId == order.OrderId)).RefundedPaymentId);
        Assert.True(await db.BookingEvents.AnyAsync(e => e.BookingId == bookingId && e.Title == "Extra payment refunded" && e.IsVisibleToCustomer));

        // Never refunded twice, and no seats were ever taken.
        await s.Payments.ReconcileAsync(bookingId);
        Assert.Equal(1, s.Razorpay.RefundCallCount);
        Assert.Equal(10, await SeatsLeftAsync(db, slot));
    }

    [Fact]
    public async Task SecondPaymentOnAnotherOrder_ForAnAlreadyPaidBooking_IsRefunded()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var bookingId = await RequestAsync(s, trip, slot, customer);
        var first = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Partial, null);
        var second = await s.Payments.CreateOrderAsync(customer.CustomerId, bookingId, BookingPaymentPlan.Full, null);
        var firstPayment = s.Razorpay.Pay(first.OrderId, first.AmountPaise);
        var secondPayment = s.Razorpay.Pay(second.OrderId, second.AmountPaise);

        await s.Payments.VerifyAndConfirmAsync(customer.CustomerId, bookingId, new VerifyPaymentRequest(second.OrderId, secondPayment, "sig"));
        await s.Payments.ReconcileAsync(bookingId);

        var booking = await ReloadAsync(db, bookingId);
        Assert.Equal(secondPayment, booking.RazorpayPaymentId);
        Assert.Equal(1, s.Razorpay.RefundCallCount);
        Assert.Equal(firstPayment, s.Razorpay.LastRefundedPaymentId);
        Assert.Equal(8, await SeatsLeftAsync(db, slot));
    }

    [Fact]
    public async Task Reminders_GoOutAt15And30Minutes_OnlyTwice_AndNotOncePaid()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var (trip, slot, customer) = await SeedAsync(db);
        var unpaid = await RequestAsync(s, trip, slot, customer);
        var reminders = new PaymentReminderService(db, s.WhatsApp, new NoEmail(), Options.Create(new EmailOptions { SiteUrl = "https://example.test" }), NullLogger<PaymentReminderService>.Instance);

        // The job runs in a fresh scope every minute; here one context is reused, so drop its cached rows.
        async Task AgeAsync(int minutes) { db.ChangeTracker.Clear(); await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Bookings SET RequestedAt = {DateTime.UtcNow.AddMinutes(-minutes)} WHERE BookingId = {unpaid}"); }
        async Task<int> SentAsync() => (await ReloadAsync(db, unpaid)).PaymentRemindersSent;

        await AgeAsync(10);
        await reminders.SendDueRemindersAsync();
        Assert.Equal(0, await SentAsync());

        await AgeAsync(16);
        await reminders.SendDueRemindersAsync();
        await reminders.SendDueRemindersAsync(); // the next minute: not again
        Assert.Equal(1, await SentAsync());
        Assert.Contains($"https://example.test/trips/{trip.TripId}?pay={unpaid}", s.WhatsApp.LastTemplateVariable2);

        await AgeAsync(31);
        await reminders.SendDueRemindersAsync();
        Assert.Equal(2, await SentAsync());

        await AgeAsync(45);
        await reminders.SendDueRemindersAsync();
        Assert.Equal(2, await SentAsync());

        // A checkout that got paid is never reminded.
        var (trip2, slot2, customer2) = await SeedAsync(db);
        var paid = await RequestAsync(s, trip2, slot2, customer2);
        var order = await s.Payments.CreateOrderAsync(customer2.CustomerId, paid, BookingPaymentPlan.Partial, null);
        s.Razorpay.Pay(order.OrderId, order.AmountPaise);
        await s.Payments.ReconcileAsync(paid);
        db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Bookings SET RequestedAt = {DateTime.UtcNow.AddMinutes(-20)} WHERE BookingId = {paid}");
        await reminders.SendDueRemindersAsync();
        Assert.Equal(0, (await ReloadAsync(db, paid)).PaymentRemindersSent);
    }

    private sealed class NoEmail : IBookingEmailService
    {
        public Task SendBookingConfirmedAsync(Booking booking, Trip trip, TripDateSlot slot, Customer customer, BookingConfirmedWhatsAppMessage details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendInvoiceAsync(int customerId, int bookingId, string toEmail, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendBookingCancelledAsync(int bookingId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendPaymentReceivedAsync(int bookingId, decimal amount, string methodLabel, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SendPaymentReminderAsync(int bookingId, decimal amountDue, int minutesLeft, string payUrl, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
