using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Coupons;
using GhumoOdisha.Application.Coupons.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Coupon holder commission: earned on active bookings, paid out manually, netted after cancellations.</summary>
public class CouponPayoutTests
{
    private static BookingService CreateBookingService(GhumoOdishaDbContext db) =>
        new(db, new FakeRazorpayService(), new FakeWhatsAppService(),
            Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }),
            NullLogger<BookingService>.Instance);

    /// <summary>A coupon at ₹200/seat commission with one confirmed booking per entry in <paramref name="seatsPerBooking"/>.</summary>
    private static async Task<(CouponCode Coupon, List<int> BookingIds)> SeedCouponWithBookingsAsync(GhumoOdishaDbContext db, params int[] seatsPerBooking)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"Coupon Trip {Guid.NewGuid():N}", Description = "Automated test trip.", AmountPerPerson = 1000m, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.Trips.Add(trip);
        await db.SaveChangesAsync();
        var slot = new TripDateSlot { TripId = trip.TripId, StartDate = DateOnly.FromDateTime(now.AddDays(30)), EndDate = DateOnly.FromDateTime(now.AddDays(31)), TotalSeats = 50, AvailableSeats = 50, Status = TripDateSlotStatus.Active, CreatedAt = now, UpdatedAt = now };
        db.TripDateSlots.Add(slot);
        var coupon = new CouponCode { Code = $"P{Guid.NewGuid():N}"[..12].ToUpperInvariant(), HolderName = "Agent Rahul", DiscountAmount = 100m, CommissionPerSeat = 200m, IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.CouponCodes.Add(coupon);
        await db.SaveChangesAsync();

        var bookings = CreateBookingService(db);
        var coupons = new CouponService(db);
        var ids = new List<int>();
        foreach (var seats in seatsPerBooking)
        {
            var customer = new Customer { Name = "Coupon Customer", PhoneNumber = TestDb.RandomPhoneNumber(), IsVerified = true, CreatedAt = now, UpdatedAt = now };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var r = await bookings.RequestBookingAsync(customer.CustomerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, seats, null, AgreedToTerms: true));
            await bookings.ConfirmBookingAsync(r.Booking.BookingId, new ConfirmBookingRequest(0m, 100m));
            await coupons.RedeemAsync(coupon.CouponCodeId, customer.CustomerId, r.Booking.BookingId, 100m);
            ids.Add(r.Booking.BookingId);
        }

        return (coupon, ids);
    }

    private static async Task<AdminCouponDto> SummaryAsync(CouponService service, int couponCodeId) =>
        (await service.GetAllAsync()).Single(c => c.CouponCodeId == couponCodeId);

    [Fact]
    public async Task Payout_ReducesBalance_AndCannotExceedIt()
    {
        await using var db = TestDb.CreateContext();
        var (coupon, _) = await SeedCouponWithBookingsAsync(db, 2, 3);   // 5 seats × ₹200 = ₹1,000 earned
        var service = new CouponService(db);

        await service.AddPayoutAsync(coupon.CouponCodeId, new AddCouponPayoutRequest(600m, PaymentMethod.Upi, "UTR1", null));

        var summary = await SummaryAsync(service, coupon.CouponCodeId);
        Assert.Equal(1000m, summary.CommissionEarned);
        Assert.Equal(600m, summary.CommissionPaid);
        Assert.Equal(400m, summary.BalanceDue);
        Assert.Equal(2, summary.ActiveBookingCount);

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.AddPayoutAsync(coupon.CouponCodeId, new AddCouponPayoutRequest(401m, PaymentMethod.Cash, null, null)));
        Assert.Single(await service.GetPayoutsAsync(coupon.CouponCodeId));
    }

    [Fact]
    public async Task CancellationAfterPayout_ReversesCommission_AndBalanceGoesNegative()
    {
        await using var db = TestDb.CreateContext();
        var (coupon, bookingIds) = await SeedCouponWithBookingsAsync(db, 2, 3);   // ₹1,000 earned
        var service = new CouponService(db);
        await service.AddPayoutAsync(coupon.CouponCodeId, new AddCouponPayoutRequest(1000m, PaymentMethod.BankTransfer, "UTR2", null));

        // The 3-seat booking is cancelled after the holder was already paid for it.
        await CreateBookingService(db).CancelBookingAsync(bookingIds[1], new CancelBookingRequest(null));

        var summary = await SummaryAsync(service, coupon.CouponCodeId);
        Assert.Equal(400m, summary.CommissionEarned);
        Assert.Equal(600m, summary.CommissionReversed);
        Assert.Equal(1, summary.CancelledBookingCount);
        Assert.Equal(-600m, summary.BalanceDue);   // netted against their next commission

        await Assert.ThrowsAsync<ValidationAppException>(() =>
            service.AddPayoutAsync(coupon.CouponCodeId, new AddCouponPayoutRequest(1m, PaymentMethod.Cash, null, null)));

        var rows = await service.GetBookingsAsync(coupon.CouponCodeId);
        var cancelled = rows.Single(b => b.BookingId == bookingIds[1]);
        Assert.Equal(0m, cancelled.CommissionEarned);
        Assert.Equal(600m, cancelled.CommissionAmount);
    }

    [Fact]
    public async Task RemovePayout_RestoresBalance()
    {
        await using var db = TestDb.CreateContext();
        var (coupon, _) = await SeedCouponWithBookingsAsync(db, 1);   // ₹200
        var service = new CouponService(db);
        await service.AddPayoutAsync(coupon.CouponCodeId, new AddCouponPayoutRequest(200m, PaymentMethod.Cash, null, null));
        var payout = Assert.Single(await service.GetPayoutsAsync(coupon.CouponCodeId));

        await service.RemovePayoutAsync(coupon.CouponCodeId, payout.CouponPayoutId);

        Assert.Equal(200m, (await SummaryAsync(service, coupon.CouponCodeId)).BalanceDue);
    }

    [Fact]
    public async Task PartnerSummary_ShowsTotalsAndPayouts_ButNothingAboutWhoBooked()
    {
        await using var db = TestDb.CreateContext();
        var (coupon, bookingIds) = await SeedCouponWithBookingsAsync(db, 2, 3);   // ₹1,000 earned
        var service = new CouponService(db);
        await service.AddPayoutAsync(coupon.CouponCodeId, new AddCouponPayoutRequest(300m, PaymentMethod.Upi, "UTR-SECRET", null));
        await CreateBookingService(db).CancelBookingAsync(bookingIds[0], new CancelBookingRequest(null));

        // Case-insensitive, like the customer-facing coupon field.
        var summary = await service.GetPartnerSummaryAsync(coupon.Code.ToLowerInvariant());

        Assert.Equal(coupon.Code, summary.Code);
        Assert.Equal(1, summary.ActiveBookings);
        Assert.Equal(1, summary.CancelledBookings);
        Assert.Equal(3, summary.SeatsTravelling);
        Assert.Equal(600m, summary.CommissionEarned);
        Assert.Equal(400m, summary.CommissionReversed);
        Assert.Equal(300m, summary.CommissionPaid);
        Assert.Equal(300m, summary.BalanceDue);
        Assert.Equal(2, summary.Activity.Count);
        Assert.Single(summary.Payouts);

        // The public payload never carries customer or booking identifiers, or payout references.
        var json = System.Text.Json.JsonSerializer.Serialize(summary);
        Assert.DoesNotContain("Coupon Customer", json);
        Assert.DoesNotContain("UTR-SECRET", json);
        Assert.DoesNotContain("customer", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bookingId", json, StringComparison.OrdinalIgnoreCase);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPartnerSummaryAsync("NO-SUCH-CODE-123"));
    }

    [Fact]
    public async Task DeleteCoupon_WithPayoutHistory_OnlyDeactivates()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var coupon = new CouponCode { Code = $"D{Guid.NewGuid():N}"[..12].ToUpperInvariant(), HolderName = "Agent", DiscountAmount = 50m, CommissionPerSeat = 100m, IsActive = true, CreatedAt = now, UpdatedAt = now };
        db.CouponCodes.Add(coupon);
        await db.SaveChangesAsync();
        db.CouponPayouts.Add(new CouponPayout { CouponCodeId = coupon.CouponCodeId, Amount = 100m, Method = PaymentMethod.Cash, PaidAt = now, CreatedAt = now });
        await db.SaveChangesAsync();

        await new CouponService(db).DeleteAsync(coupon.CouponCodeId);

        var after = await db.CouponCodes.AsNoTracking().SingleAsync(c => c.CouponCodeId == coupon.CouponCodeId);
        Assert.False(after.IsActive);
    }
}
