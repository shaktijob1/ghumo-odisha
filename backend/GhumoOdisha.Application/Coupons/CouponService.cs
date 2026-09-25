using System.Data;
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

        var everRedeemed = await db.CouponRedemptions.AnyAsync(r => r.CouponCodeId == couponCodeId, cancellationToken)
            || await db.CouponPayouts.AnyAsync(p => p.CouponCodeId == couponCodeId, cancellationToken);
        if (everRedeemed)
        {
            // Keep the audit trail (invoices, payout history) intact — deactivate instead of deleting.
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
        var rows = await db.CouponCodes
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                Coupon = c,
                RedemptionCount = c.Redemptions.Count,
                NewCustomerCount = c.Redemptions.Count(r => r.IsNewCustomer),
                ExistingCustomerCount = c.Redemptions.Count(r => !r.IsNewCustomer),
                ActiveCount = c.Redemptions.Count(r => r.Booking.BookingStatus != BookingStatus.Cancelled && r.Booking.BookingStatus != BookingStatus.Rejected),
                ActiveSeats = c.Redemptions
                    .Where(r => r.Booking.BookingStatus != BookingStatus.Cancelled && r.Booking.BookingStatus != BookingStatus.Rejected)
                    .Sum(r => (int?)r.NumberOfSeats) ?? 0,
                Earned = c.Redemptions
                    .Where(r => r.Booking.BookingStatus != BookingStatus.Cancelled && r.Booking.BookingStatus != BookingStatus.Rejected)
                    .Sum(r => (decimal?)r.CommissionAmount) ?? 0m,
                Reversed = c.Redemptions
                    .Where(r => r.Booking.BookingStatus == BookingStatus.Cancelled || r.Booking.BookingStatus == BookingStatus.Rejected)
                    .Sum(r => (decimal?)r.CommissionAmount) ?? 0m,
                Paid = c.Payouts.Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminCouponDto(
                r.Coupon.CouponCodeId,
                r.Coupon.Code,
                r.Coupon.HolderName,
                r.Coupon.DiscountAmount,
                r.Coupon.CommissionPerSeat,
                r.Coupon.ValidFrom,
                r.Coupon.ValidUntil,
                r.Coupon.IsActive,
                r.Coupon.IsFirstTimeCustomerOnly,
                r.RedemptionCount,
                r.NewCustomerCount,
                r.ExistingCustomerCount,
                r.ActiveSeats,
                r.Earned,
                r.Coupon.CreatedAt,
                r.ActiveCount,
                r.RedemptionCount - r.ActiveCount,
                r.Reversed,
                r.Paid,
                r.Earned - r.Paid))
            .ToList();
    }

    public async Task<IReadOnlyList<CouponPayoutDto>> GetPayoutsAsync(int couponCodeId, CancellationToken cancellationToken = default)
    {
        if (!await db.CouponCodes.AnyAsync(c => c.CouponCodeId == couponCodeId, cancellationToken))
        {
            throw new NotFoundException("Coupon not found.");
        }

        return await db.CouponPayouts.AsNoTracking()
            .Where(p => p.CouponCodeId == couponCodeId)
            .OrderByDescending(p => p.PaidAt).ThenByDescending(p => p.CouponPayoutId)
            .Select(p => new CouponPayoutDto(p.CouponPayoutId, p.Amount, p.Method, p.Reference, p.Notes, p.PaidAt))
            .ToListAsync(cancellationToken);
    }

    public async Task AddPayoutAsync(int couponCodeId, AddCouponPayoutRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            // Locked so two admins paying the same holder at once can't both pass the balance check.
            var coupon = await db.CouponCodes
                .FromSqlInterpolated($"SELECT * FROM CouponCodes WHERE CouponCodeId = {couponCodeId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Coupon not found.");

            var amount = decimal.Round(request.Amount, 2);
            if (amount <= 0)
            {
                throw new ValidationAppException(["Amount must be greater than zero."]);
            }

            var balance = await GetBalanceAsync(couponCodeId, cancellationToken);
            if (amount > balance)
            {
                throw new ValidationAppException([balance <= 0
                    ? $"Nothing is due to {coupon.HolderName} right now."
                    : $"Amount can't be more than the Rs {balance:N2} currently due to {coupon.HolderName}."]);
            }

            var now = DateTime.UtcNow;
            db.CouponPayouts.Add(new CouponPayout
            {
                CouponCodeId = couponCodeId,
                Amount = amount,
                Method = request.Method,
                Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                PaidAt = request.PaidAt?.ToUniversalTime() ?? now,
                CreatedAt = now
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RemovePayoutAsync(int couponCodeId, int couponPayoutId, CancellationToken cancellationToken = default)
    {
        var payout = await db.CouponPayouts
            .FirstOrDefaultAsync(p => p.CouponPayoutId == couponPayoutId && p.CouponCodeId == couponCodeId, cancellationToken)
            ?? throw new NotFoundException("Payout not found.");

        db.CouponPayouts.Remove(payout);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PartnerCouponSummaryDto> GetPartnerSummaryAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(code ?? "");
        var coupon = await db.CouponCodes.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken)
            ?? throw new NotFoundException("We couldn't find that coupon code. Check the spelling and try again.");

        // Deliberately no customer or booking identifiers in this projection — the page is public.
        var redemptions = await db.CouponRedemptions.AsNoTracking()
            .Where(r => r.CouponCodeId == coupon.CouponCodeId)
            .OrderByDescending(r => r.RedeemedAt)
            .Select(r => new
            {
                r.RedeemedAt,
                TripTitle = r.Booking.Trip.Title,
                TravelDate = r.Booking.TripDateSlot.StartDate,
                r.NumberOfSeats,
                r.CommissionAmount,
                Counts = r.Booking.BookingStatus != BookingStatus.Cancelled && r.Booking.BookingStatus != BookingStatus.Rejected
            })
            .ToListAsync(cancellationToken);

        var payouts = await db.CouponPayouts.AsNoTracking()
            .Where(p => p.CouponCodeId == coupon.CouponCodeId)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new PartnerPayoutDto(p.PaidAt, p.Amount, p.Method))
            .ToListAsync(cancellationToken);

        var earned = redemptions.Where(r => r.Counts).Sum(r => r.CommissionAmount);
        var paid = payouts.Sum(p => p.Amount);

        return new PartnerCouponSummaryDto(
            coupon.Code,
            coupon.HolderName,
            coupon.DiscountAmount,
            coupon.CommissionPerSeat,
            coupon.IsActive,
            coupon.ValidUntil,
            redemptions.Count(r => r.Counts),
            redemptions.Count(r => !r.Counts),
            redemptions.Where(r => r.Counts).Sum(r => r.NumberOfSeats),
            earned,
            redemptions.Where(r => !r.Counts).Sum(r => r.CommissionAmount),
            paid,
            earned - paid,
            payouts,
            redemptions.Select(r => new PartnerActivityDto(r.RedeemedAt, r.TripTitle, r.TravelDate, r.NumberOfSeats, r.CommissionAmount, r.Counts)).ToList());
    }

    private async Task<decimal> GetBalanceAsync(int couponCodeId, CancellationToken cancellationToken)
    {
        var earned = await db.CouponRedemptions
            .Where(r => r.CouponCodeId == couponCodeId
                && r.Booking.BookingStatus != BookingStatus.Cancelled
                && r.Booking.BookingStatus != BookingStatus.Rejected)
            .SumAsync(r => (decimal?)r.CommissionAmount, cancellationToken) ?? 0m;
        var paid = await db.CouponPayouts
            .Where(p => p.CouponCodeId == couponCodeId)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;
        return earned - paid;
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
