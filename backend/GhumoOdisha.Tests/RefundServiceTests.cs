using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Refunds;
using GhumoOdisha.Application.Refunds.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Cancel queues a refund; the admin issues it (Razorpay or manual) and marks it settled.</summary>
public class RefundServiceTests
{
    private static BookingService CreateBookingService(GhumoOdishaDbContext db, FakeRazorpayService razorpay) =>
        new(db, razorpay, new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    private static RefundService CreateRefundService(GhumoOdishaDbContext db, FakeRazorpayService razorpay, FakeEmailSender? email = null) =>
        new(db, razorpay, NullLogger<RefundService>.Instance, email);

    /// <summary>A confirmed booking (2 seats × ₹1000), optionally paid online, then cancelled by the customer.</summary>
    private static async Task<(int BookingId, int RefundId, int CustomerId)> CancelledPaidBookingAsync(
        GhumoOdishaDbContext db, FakeRazorpayService razorpay, decimal advance, string? razorpayPaymentId, string? customerEmail = null)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Refund Trip {Guid.NewGuid():N}", Description = "Test", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();
        var slot = new TripDateSlot
        {
            TripId = trip.TripId, StartDate = DateOnly.FromDateTime(now.AddDays(30)), EndDate = DateOnly.FromDateTime(now.AddDays(32)),
            TotalSeats = 10, AvailableSeats = 10, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now
        };
        var customer = new Customer { Name = "Refund Customer", PhoneNumber = TestDb.RandomPhoneNumber(), Email = customerEmail, IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.TripDateSlots.Add(slot);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var bookings = CreateBookingService(db, razorpay);
        var requested = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 2, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(advance, 0, razorpayPaymentId));
        db.ChangeTracker.Clear();
        await bookings.CancelOwnBookingAsync(customer.CustomerId, requested.Booking.BookingId);
        db.ChangeTracker.Clear();

        var refundId = await db.BookingRefunds.Where(r => r.BookingId == requested.Booking.BookingId).Select(r => r.BookingRefundId).SingleAsync();
        return (requested.Booking.BookingId, refundId, customer.CustomerId);
    }

    [Fact]
    public async Task CustomerCancel_QueuesPendingRefund_WithoutCallingRazorpay_AndCustomerSeesIt()
    {
        await using var db = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var (bookingId, _, customerId) = await CancelledPaidBookingAsync(db, razorpay, 1500m, "pay_refund_1");

        Assert.Equal(0, razorpay.RefundCallCount);

        var view = await CreateBookingService(db, razorpay).GetCustomerBookingDetailAsync(customerId, bookingId);
        Assert.Equal(PaymentStatus.RefundPending, view.PaymentStatus);
        Assert.NotNull(view.Refund);
        Assert.Equal(RefundStatus.Pending, view.Refund!.Status);
        Assert.Equal(1500m, view.Refund.Amount);
    }

    [Fact]
    public async Task RazorpayRefund_PartialAmount_ThenSettle_MarksBookingRefunded_AndEmailsCustomer()
    {
        await using var db = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var email = new FakeEmailSender();
        var (bookingId, refundId, _) = await CancelledPaidBookingAsync(db, razorpay, 1500m, "pay_refund_2", $"r-{Guid.NewGuid():N}@example.com");
        var refunds = CreateRefundService(db, razorpay, email);

        var issued = await refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1200m, "₹300 cancellation fee"));

        Assert.Equal(1, razorpay.RefundCallCount);
        Assert.Equal("pay_refund_2", razorpay.LastRefundedPaymentId);
        Assert.Equal(120000, razorpay.LastRefundAmountPaise);
        Assert.Equal(RefundStatus.Processing, issued.Status);
        Assert.Equal(1200m, issued.Amount);
        Assert.Equal(razorpay.LastRefundId, issued.Reference);

        // Still processing: the customer sees "refund processing", not "refunded".
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(PaymentStatus.RefundPending, booking.PaymentStatus);

        db.ChangeTracker.Clear();
        var settled = await refunds.SettleAsync(refundId, new SettleRefundRequest("Razorpay shows processed"));

        Assert.Equal(RefundStatus.Settled, settled.Status);
        Assert.NotNull(settled.SettledAt);
        booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(PaymentStatus.Refunded, booking.PaymentStatus);

        var sent = Assert.Single(email.Sent);
        Assert.Contains("₹1,200", sent.HtmlBody);
    }

    [Fact]
    public async Task ManualRefund_RecordsMethodAndReference_ThenSettles()
    {
        await using var db = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var (bookingId, refundId, _) = await CancelledPaidBookingAsync(db, razorpay, 2000m, null);
        var refunds = CreateRefundService(db, razorpay);

        // Paid offline — a Razorpay refund isn't possible.
        await Assert.ThrowsAsync<ConflictException>(() => refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(2000m, null)));

        var issued = await refunds.RecordManualRefundAsync(refundId, new RecordManualRefundRequest(2000m, PaymentMethod.Upi, "UTR123456", null));
        Assert.Equal(RefundStatus.Processing, issued.Status);
        Assert.Equal(PaymentMethod.Upi, issued.Method);
        Assert.Equal("UTR123456", issued.Reference);
        Assert.Equal(0, razorpay.RefundCallCount);

        db.ChangeTracker.Clear();
        await refunds.SettleAsync(refundId, new SettleRefundRequest(null));
        var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.BookingId == bookingId);
        Assert.Equal(PaymentStatus.Refunded, booking.PaymentStatus);
    }

    [Fact]
    public async Task Refund_CannotExceedPaid_CannotIssueTwice_AndCannotSettleBeforeIssuing()
    {
        await using var db = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var (_, refundId, _) = await CancelledPaidBookingAsync(db, razorpay, 1000m, "pay_refund_3");
        var refunds = CreateRefundService(db, razorpay);

        await Assert.ThrowsAsync<ConflictException>(() => refunds.SettleAsync(refundId, new SettleRefundRequest(null)));
        await Assert.ThrowsAsync<ValidationAppException>(() => refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000.01m, null)));
        await Assert.ThrowsAsync<ValidationAppException>(() => refunds.RecordManualRefundAsync(refundId, new RecordManualRefundRequest(5000m, PaymentMethod.Cash, null, null)));

        await refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000m, null));
        db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ConflictException>(() => refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000m, null)));
        await Assert.ThrowsAsync<ConflictException>(() => refunds.RecordManualRefundAsync(refundId, new RecordManualRefundRequest(1000m, PaymentMethod.Cash, null, null)));
        Assert.Equal(1, razorpay.RefundCallCount); // never refunded twice
    }

    [Fact]
    public async Task RazorpayFailure_LeavesRefundPending_SoItCanBeRetried()
    {
        await using var db = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var (_, refundId, _) = await CancelledPaidBookingAsync(db, razorpay, 1000m, "pay_refund_4");
        var refunds = CreateRefundService(db, razorpay);

        razorpay.ShouldFailRefund = true;
        await Assert.ThrowsAsync<ConflictException>(() => refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000m, null)));

        var refund = await db.BookingRefunds.AsNoTracking().SingleAsync(r => r.BookingRefundId == refundId);
        Assert.Equal(RefundStatus.Pending, refund.Status);

        razorpay.ShouldFailRefund = false;
        db.ChangeTracker.Clear();
        var issued = await refunds.IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000m, null));
        Assert.Equal(RefundStatus.Processing, issued.Status);
    }

    [Fact]
    public async Task ConcurrentIssue_OnlyOneRazorpayRefundHappens()
    {
        await using var setupDb = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var (_, refundId, _) = await CancelledPaidBookingAsync(setupDb, razorpay, 1000m, "pay_refund_5");

        // Two admins, two separate DbContexts, clicking at the same moment.
        await using var dbA = TestDb.CreateContext();
        await using var dbB = TestDb.CreateContext();
        var results = await Task.WhenAll(
            Try(() => CreateRefundService(dbA, razorpay).IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000m, null))),
            Try(() => CreateRefundService(dbB, razorpay).IssueRazorpayRefundAsync(refundId, new IssueRazorpayRefundRequest(1000m, null))));

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Equal(1, razorpay.RefundCallCount);

        static async Task<bool> Try(Func<Task> action)
        {
            try { await action(); return true; }
            catch (ConflictException) { return false; }
        }
    }

    [Fact]
    public async Task WaivedCancel_QueuesNoRefund()
    {
        await using var db = TestDb.CreateContext();
        var razorpay = new FakeRazorpayService();
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Waive Trip {Guid.NewGuid():N}", Description = "Test", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();
        var slot = new TripDateSlot { TripId = trip.TripId, StartDate = DateOnly.FromDateTime(now.AddDays(30)), EndDate = DateOnly.FromDateTime(now.AddDays(31)), TotalSeats = 5, AvailableSeats = 5, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now };
        var customer = new Customer { Name = "Waive", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
        db.TripDateSlots.Add(slot);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var bookings = CreateBookingService(db, razorpay);
        var requested = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, 1, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(requested.Booking.BookingId, new ConfirmBookingRequest(500m));
        db.ChangeTracker.Clear();
        await bookings.CancelBookingAsync(requested.Booking.BookingId, new CancelBookingRequest(null, WaiveRefund: true, Reason: "No-show"));

        Assert.False(await db.BookingRefunds.AnyAsync(r => r.BookingId == requested.Booking.BookingId));
    }
}
