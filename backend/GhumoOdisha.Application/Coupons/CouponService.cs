using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Coupons.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
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
            DiscountAmount = request.DiscountAmount,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            IsActive = true,
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

        coupon.DiscountAmount = request.DiscountAmount;
        coupon.ValidFrom = request.ValidFrom;
        coupon.ValidUntil = request.ValidUntil;
        coupon.IsActive = request.IsActive;
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
                c.DiscountAmount,
                c.ValidFrom,
                c.ValidUntil,
                c.IsActive,
                c.Redemptions.Count,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return coupons;
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

        return new CouponValidation(coupon.CouponCodeId, coupon.DiscountAmount);
    }

    public async Task RedeemAsync(int couponCodeId, int customerId, int bookingId, decimal discountAmount, CancellationToken cancellationToken = default)
    {
        db.CouponRedemptions.Add(new CouponRedemption
        {
            CouponCodeId = couponCodeId,
            CustomerId = customerId,
            BookingId = bookingId,
            DiscountAmount = discountAmount,
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

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
