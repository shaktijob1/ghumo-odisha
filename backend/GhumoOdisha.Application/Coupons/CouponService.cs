using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Coupons.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Coupons;

public class CouponService(IGhumoOdishaDbContext db) : ICouponService
{
    public async Task<int> CreateAsync(AdminCreateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var code = Normalize(request.Code);

        var exists = await db.CouponCodes.AnyAsync(c => c.Code == code, cancellationToken);
        if (exists)
        {
            throw new ConflictException("A coupon with this code already exists.");
        }

        var now = DateTime.UtcNow;
        var coupon = new CouponCode
        {
            Code = code,
            HolderName = request.HolderName.Trim(),
            DiscountAmount = request.DiscountAmount,
            CommissionPerSeat = request.CommissionPerSeat,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            IsActive = true,
            IsFirstTimeCustomerOnly = request.IsFirstTimeCustomerOnly,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.CouponCodes.Add(coupon);
        await db.SaveChangesAsync(cancellationToken);
        return coupon.CouponCodeId;
    }

    public async Task UpdateAsync(int couponCodeId, AdminUpdateCouponRequest request, CancellationToken cancellationToken = default)
    {
        var coupon = await db.CouponCodes.FirstOrDefaultAsync(c => c.CouponCodeId == couponCodeId, cancellationToken)
            ?? throw new NotFoundException("Coupon not found.");

        // A new commission rate only applies to future redemptions — each redemption row keeps
        // the commission it earned at the time.
        coupon.HolderName = request.HolderName.Trim();
        coupon.DiscountAmount = request.DiscountAmount;
        coupon.CommissionPerSeat = request.CommissionPerSeat;
        coupon.ValidFrom = request.ValidFrom;
        coupon.ValidUntil = request.ValidUntil;
        coupon.IsActive = request.IsActive;
        coupon.IsFirstTimeCustomerOnly = request.IsFirstTimeCustomerOnly;
        coupon.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int couponCodeId, CancellationToken cancellationToken = default)
    {
        var coupon = await db.CouponCodes.FirstOrDefaultAsync(c => c.CouponCodeId == couponCodeId, cancellationToken)
            ?? throw new NotFoundException("Coupon not found.");

        var everRedeemed = await db.CouponRedemptions.AnyAsync(r => r.CouponCodeId == couponCodeId, cancellationToken);
        if (everRedeemed)
        {
            // Keep the audit trail (and any invoice referencing it) intact — deactivate instead of deleting.
            coupon.IsActive = false;
            coupon.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        db.CouponCodes.Remove(coupon);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminCouponDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var coupons = await db.CouponCodes
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AdminCouponDto(
                c.CouponCodeId,
                c.Code,
                c.HolderName,
                c.DiscountAmount,
                c.CommissionPerSeat,
                c.ValidFrom,
                c.ValidUntil,
                c.IsActive,
                c.IsFirstTimeCustomerOnly,
                c.Redemptions.Count,
                c.Redemptions.Count(r => r.IsNewCustomer),
                c.Redemptions.Count(r => !r.IsNewCustomer),
                c.Redemptions.Sum(r => (int?)r.NumberOfSeats) ?? 0,
                c.Redemptions
                    .Where(r => r.Booking.BookingStatus != BookingStatus.Cancelled
                        && r.Booking.BookingStatus != BookingStatus.Rejected)
                    .Sum(r => (decimal?)r.CommissionAmount) ?? 0m,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return coupons;
    }

    public async Task<IReadOnlyList<AdminCouponBookingDto>> GetBookingsAsync(int couponCodeId, CancellationToken cancellationToken = default)
    {
        var exists = await db.CouponCodes.AnyAsync(c => c.CouponCodeId == couponCodeId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Coupon not found.");
        }

        return await db.CouponRedemptions
            .Where(r => r.CouponCodeId == couponCodeId)
            .OrderByDescending(r => r.RedeemedAt)
            .Select(r => new AdminCouponBookingDto(
                r.BookingId,
                r.CustomerId,
                r.Customer.Name,
                r.Customer.PhoneNumber,
                r.IsNewCustomer,
                r.Booking.Trip.Title,
                r.Booking.TripDateSlot.StartDate,
                r.NumberOfSeats,
                r.Booking.TotalAmount,
                r.DiscountAmount,
                r.CommissionAmount,
                r.Booking.BookingStatus == BookingStatus.Cancelled || r.Booking.BookingStatus == BookingStatus.Rejected
                    ? 0m
                    : r.CommissionAmount,
                r.Booking.BookingStatus,
                r.RedeemedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<CouponValidation> ValidateAsync(string code, int customerId, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(code);

        var coupon = await db.CouponCodes.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken)
            ?? throw new NotFoundException("This coupon code doesn't exist.");

        if (!coupon.IsActive)
        {
            throw new ConflictException("This coupon is no longer active.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (coupon.ValidFrom.HasValue && today < coupon.ValidFrom.Value)
        {
            throw new ConflictException("This coupon isn't active yet.");
        }

        if (coupon.ValidUntil.HasValue && today > coupon.ValidUntil.Value)
        {
            throw new ConflictException("This coupon has expired.");
        }

        var alreadyUsed = await db.CouponRedemptions.AnyAsync(
            r => r.CouponCodeId == coupon.CouponCodeId && r.CustomerId == customerId, cancellationToken);
        if (alreadyUsed)
        {
            throw new ConflictException("You've already used this coupon.");
        }

        // Coupons are applied while paying for a booking that already exists (status Requested),
        // so "first-time" means no booking was ever confirmed — not "no booking rows at all".
        if (coupon.IsFirstTimeCustomerOnly && await HasConfirmedBookingAsync(customerId, excludeBookingId: null, cancellationToken))
        {
            throw new ConflictException("This coupon is only for first-time customers.");
        }

        return new CouponValidation(coupon.CouponCodeId, coupon.DiscountAmount);
    }

    public async Task RedeemAsync(int couponCodeId, int customerId, int bookingId, decimal discountAmount, CancellationToken cancellationToken = default)
    {
        var commissionPerSeat = await db.CouponCodes
            .Where(c => c.CouponCodeId == couponCodeId)
            .Select(c => (decimal?)c.CommissionPerSeat)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Coupon not found.");

        // Seats come from the booking row in the database, never from the client.
        var seats = await db.Bookings
            .Where(b => b.BookingId == bookingId && b.CustomerId == customerId)
            .Select(b => (int?)b.NumberOfSeats)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        var isNewCustomer = !await HasConfirmedBookingAsync(customerId, excludeBookingId: bookingId, cancellationToken);

        db.CouponRedemptions.Add(new CouponRedemption
        {
            CouponCodeId = couponCodeId,
            CustomerId = customerId,
            BookingId = bookingId,
            DiscountAmount = discountAmount,
            NumberOfSeats = seats,
            IsNewCustomer = isNewCustomer,
            CommissionAmount = commissionPerSeat * seats,
            RedeemedAt = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("This coupon has already been used.");
        }
    }

    /// <summary>True when the customer has a booking (other than <paramref name="excludeBookingId"/>)
    /// that was ever confirmed — a later cancellation doesn't make them a new customer again.</summary>
    private Task<bool> HasConfirmedBookingAsync(int customerId, int? excludeBookingId, CancellationToken cancellationToken)
        => db.Bookings.AnyAsync(
            b => b.CustomerId == customerId && b.ConfirmedAt != null && b.BookingId != excludeBookingId,
            cancellationToken);

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
