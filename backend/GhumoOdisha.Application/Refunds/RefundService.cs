using System.Data;
using System.Linq.Expressions;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Notifications;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Application.Refunds.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Refunds;

public interface IRefundService
{
    Task<PagedResult<AdminRefundDto>> GetRefundsAsync(RefundStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<RefundCountsDto> GetCountsAsync(CancellationToken cancellationToken = default);

    Task<AdminRefundDto> IssueRazorpayRefundAsync(int refundId, IssueRazorpayRefundRequest request, CancellationToken cancellationToken = default);

    Task<AdminRefundDto> RecordManualRefundAsync(int refundId, RecordManualRefundRequest request, CancellationToken cancellationToken = default);

    Task<AdminRefundDto> SettleAsync(int refundId, SettleRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>Razorpay's live status for an online refund ("pending", "processed", "failed"), or null for a manual one.</summary>
    Task<string?> GetGatewayStatusAsync(int refundId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The admin's refund desk. Cancelling a paid booking only queues a Pending refund (see
/// BookingService.CancelInternalAsync); here the admin issues it — through Razorpay or by recording
/// a manual transfer (→ Processing) — and marks it Settled once the money has reached the customer,
/// which is when the booking's payment status finally becomes Refunded.
/// </summary>
public class RefundService(
    IGhumoOdishaDbContext db,
    IRazorpayService razorpay,
    ILogger<RefundService> logger,
    IEmailSender? emailSender = null,
    IOptions<EmailOptions>? emailOptions = null) : IRefundService
{
    internal static readonly Expression<Func<BookingRefund, AdminRefundDto>> ToAdminDto = r => new AdminRefundDto(
        r.BookingRefundId,
        r.BookingId,
        r.AmountPaid,
        r.Amount,
        r.Booking.Payments.Where(p => p.Method == PaymentMethod.Razorpay).Sum(p => (decimal?)p.Amount) ?? 0m,
        r.Status,
        r.Method,
        r.Reference,
        r.Notes,
        r.RequestedBy,
        r.RequestedAt,
        r.InitiatedAt,
        r.SettledAt,
        r.Booking.Customer.Name,
        r.Booking.Customer.PhoneNumber,
        r.Booking.Customer.Email,
        r.Booking.Trip.Title,
        r.Booking.TripDateSlot.StartDate,
        r.Booking.NumberOfSeats,
        r.Booking.CancellationReason,
        r.Booking.CancelledAt);

    public async Task<PagedResult<AdminRefundDto>> GetRefundsAsync(RefundStatus? status, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.BookingRefunds.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var bookingId = int.TryParse(term.Replace("GO-", "", StringComparison.OrdinalIgnoreCase), out var id) ? id : (int?)null;
            query = query.Where(r =>
                r.BookingId == bookingId ||
                r.Booking.Customer.Name.Contains(term) ||
                (r.Booking.Customer.PhoneNumber != null && r.Booking.Customer.PhoneNumber.Contains(term)) ||
                (r.Booking.Customer.Email != null && r.Booking.Customer.Email.Contains(term)) ||
                r.Booking.Trip.Title.Contains(term));
        }

        // Pending/Processing: oldest first (work the queue in order). Settled: most recent first.
        query = status == RefundStatus.Settled
            ? query.OrderByDescending(r => r.SettledAt)
            : query.OrderBy(r => r.RequestedAt);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(ToAdminDto).ToListAsync(cancellationToken);

        return new PagedResult<AdminRefundDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<RefundCountsDto> GetCountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = await db.BookingRefunds
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        return new RefundCountsDto(
            counts.GetValueOrDefault(RefundStatus.Pending),
            counts.GetValueOrDefault(RefundStatus.Processing),
            counts.GetValueOrDefault(RefundStatus.Settled));
    }

    public async Task<AdminRefundDto> IssueRazorpayRefundAsync(int refundId, IssueRazorpayRefundRequest request, CancellationToken cancellationToken = default)
    {
        var refund = await LoadAsync(refundId, cancellationToken);
        EnsurePending(refund);

        var booking = refund.Booking;
        var onlinePaid = await db.BookingPayments
            .Where(p => p.BookingId == booking.BookingId && p.Method == PaymentMethod.Razorpay)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        if (string.IsNullOrWhiteSpace(booking.RazorpayPaymentId) || onlinePaid <= 0)
        {
            throw new ConflictException("This booking wasn't paid online — record a manual refund instead.");
        }

        var amount = ValidateAmount(request.Amount, Math.Min(onlinePaid, refund.AmountPaid),
            $"A Razorpay refund can be at most {BookingTimeline.Money(Math.Min(onlinePaid, refund.AmountPaid))} (the amount paid online).");

        // Claim the request before calling Razorpay: only one caller can move it off Pending, so a
        // double click or two admins at once can never refund the customer twice.
        await ClaimAsync(refundId, cancellationToken);

        string gatewayRefundId;
        try
        {
            gatewayRefundId = await razorpay.RefundAsync(booking.RazorpayPaymentId, ToPaise(amount), cancellationToken);
        }
        catch (Exception ex)
        {
            // Razorpay didn't create a refund — hand the request back to the queue.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE BookingRefunds SET Status = {(int)RefundStatus.Pending}, UpdatedAt = UTC_TIMESTAMP(6) WHERE BookingRefundId = {refundId} AND Status = {(int)RefundStatus.Processing} AND InitiatedAt IS NULL",
                CancellationToken.None);
            logger.LogError(ex, "Razorpay refund failed for refund {RefundId} (booking {BookingId}) — returned to Pending.", refundId, booking.BookingId);
            throw new ConflictException("Razorpay couldn't process the refund right now. Nothing was refunded — try again, or record a manual refund.");
        }

        var now = DateTime.UtcNow;
        refund.Status = RefundStatus.Processing;
        refund.Amount = amount;
        refund.Method = PaymentMethod.Razorpay;
        refund.Reference = gatewayRefundId;
        refund.Notes = Clean(request.Notes);
        refund.InitiatedAt = now;
        refund.UpdatedAt = now;
        booking.RazorpayRefundId = gatewayRefundId;
        booking.UpdatedAt = now;

        BookingTimeline.Add(db, booking, BookingEventType.RefundInitiated, "Refund initiated",
            $"{BookingTimeline.Money(amount)} refund issued to your original payment method", BookingTimeline.Admin, at: now);

        await SaveAfterClaimAsync(refundId, cancellationToken);
        logger.LogInformation("Refund {RefundId}: Razorpay refund {GatewayRefundId} issued for booking {BookingId}.", refundId, gatewayRefundId, booking.BookingId);
        return await GetDtoAsync(refundId, cancellationToken);
    }

    public async Task<AdminRefundDto> RecordManualRefundAsync(int refundId, RecordManualRefundRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Method == PaymentMethod.Razorpay)
        {
            throw new ValidationAppException(["Use \"Refund via Razorpay\" for online refunds."]);
        }

        var refund = await LoadAsync(refundId, cancellationToken);
        EnsurePending(refund);
        var amount = ValidateAmount(request.Amount, refund.AmountPaid,
            $"The refund can be at most {BookingTimeline.Money(refund.AmountPaid)} (the amount paid).");

        await ClaimAsync(refundId, cancellationToken);

        var now = DateTime.UtcNow;
        refund.Status = RefundStatus.Processing;
        refund.Amount = amount;
        refund.Method = request.Method;
        refund.Reference = Clean(request.Reference);
        refund.Notes = Clean(request.Notes);
        refund.InitiatedAt = now;
        refund.UpdatedAt = now;
        refund.Booking.UpdatedAt = now;

        BookingTimeline.Add(db, refund.Booking, BookingEventType.RefundInitiated, "Refund initiated",
            $"{BookingTimeline.Money(amount)} refund sent by {BookingTimeline.MethodLabel(request.Method)}", BookingTimeline.Admin, at: now);

        await SaveAfterClaimAsync(refundId, cancellationToken);
        return await GetDtoAsync(refundId, cancellationToken);
    }

    public async Task<AdminRefundDto> SettleAsync(int refundId, SettleRefundRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        BookingRefund refund;
        try
        {
            refund = await db.BookingRefunds
                .FromSqlInterpolated($"SELECT * FROM BookingRefunds WHERE BookingRefundId = {refundId} FOR UPDATE")
                .Include(r => r.Booking).ThenInclude(b => b.Customer)
                .Include(r => r.Booking).ThenInclude(b => b.Trip)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Refund not found.");

            if (refund.Status != RefundStatus.Processing || refund.InitiatedAt is null)
            {
                throw new ConflictException(refund.Status == RefundStatus.Settled
                    ? "This refund is already settled."
                    : "Issue the refund (Razorpay or manual) before marking it settled.");
            }

            var now = DateTime.UtcNow;
            refund.Status = RefundStatus.Settled;
            refund.SettledAt = now;
            refund.UpdatedAt = now;
            var notes = Clean(request.Notes);
            if (notes is not null)
            {
                refund.Notes = refund.Notes is null ? notes : $"{refund.Notes} — {notes}";
            }

            refund.Booking.PaymentStatus = PaymentStatus.Refunded;
            refund.Booking.UpdatedAt = now;

            BookingTimeline.Add(db, refund.Booking, BookingEventType.RefundSettled, "Refund completed",
                $"{BookingTimeline.Money(refund.Amount)} refunded", BookingTimeline.Admin, at: now);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await TrySendSettledEmailAsync(refund, cancellationToken);
        return await GetDtoAsync(refundId, cancellationToken);
    }

    public async Task<string?> GetGatewayStatusAsync(int refundId, CancellationToken cancellationToken = default)
    {
        var refund = await db.BookingRefunds.AsNoTracking().FirstOrDefaultAsync(r => r.BookingRefundId == refundId, cancellationToken)
            ?? throw new NotFoundException("Refund not found.");

        if (refund.Method != PaymentMethod.Razorpay || string.IsNullOrWhiteSpace(refund.Reference))
        {
            return null;
        }

        return await razorpay.GetRefundStatusAsync(refund.Reference, cancellationToken);
    }

    // ---------- Helpers ----------

    private async Task<BookingRefund> LoadAsync(int refundId, CancellationToken cancellationToken) =>
        await db.BookingRefunds.Include(r => r.Booking).FirstOrDefaultAsync(r => r.BookingRefundId == refundId, cancellationToken)
            ?? throw new NotFoundException("Refund not found.");

    private static void EnsurePending(BookingRefund refund)
    {
        if (refund.Status != RefundStatus.Pending)
        {
            throw new ConflictException(refund.Status == RefundStatus.Settled
                ? "This refund is already settled."
                : "This refund has already been issued.");
        }
    }

    private static decimal ValidateAmount(decimal requested, decimal max, string tooMuchMessage)
    {
        var amount = decimal.Round(requested, 2);
        if (amount <= 0)
        {
            throw new ValidationAppException(["Refund amount must be more than zero."]);
        }
        if (amount > max)
        {
            throw new ValidationAppException([tooMuchMessage]);
        }
        return amount;
    }

    /// <summary>Atomically moves the request Pending → Processing; exactly one concurrent caller wins.</summary>
    private async Task ClaimAsync(int refundId, CancellationToken cancellationToken)
    {
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE BookingRefunds SET Status = {(int)RefundStatus.Processing}, UpdatedAt = UTC_TIMESTAMP(6) WHERE BookingRefundId = {refundId} AND Status = {(int)RefundStatus.Pending}",
            cancellationToken);

        if (claimed != 1)
        {
            throw new ConflictException("This refund is already being processed.");
        }
    }

    /// <summary>
    /// Saves the issued refund's details. The row's status was already moved by <see cref="ClaimAsync"/>
    /// behind EF's back, so EF's snapshot still says Pending — that's fine: EF writes the new values.
    /// </summary>
    private async Task SaveAfterClaimAsync(int refundId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // The money has (or may have) moved but we couldn't record it. The row stays Processing with no
            // InitiatedAt, which blocks both re-issuing and settling — flagged loudly for a manual check.
            logger.LogCritical(ex, "Refund {RefundId} was issued but its details could not be saved — check Razorpay/bank records and fix manually.", refundId);
            throw;
        }
    }

    private async Task<AdminRefundDto> GetDtoAsync(int refundId, CancellationToken cancellationToken) =>
        await db.BookingRefunds.AsNoTracking().Where(r => r.BookingRefundId == refundId).Select(ToAdminDto).SingleAsync(cancellationToken);

    private static long ToPaise(decimal amount) => (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task TrySendSettledEmailAsync(BookingRefund refund, CancellationToken cancellationToken)
    {
        var customer = refund.Booking.Customer;
        if (emailSender is null || !emailSender.IsConfigured || string.IsNullOrWhiteSpace(customer.Email))
        {
            return;
        }

        try
        {
            var siteUrl = (emailOptions?.Value.SiteUrl ?? "https://www.ghumoodisha.com").TrimEnd('/');
            var (html, text) = EmailTemplates.RefundCompleted(new EmailTemplates.RefundCompletedModel(
                string.IsNullOrWhiteSpace(customer.Name) ? "there" : customer.Name.Trim(),
                $"GO-{refund.BookingId}",
                refund.Booking.Trip.Title,
                BookingTimeline.Money(refund.Amount),
                refund.Method is { } method ? BookingTimeline.MethodLabel(method) : "",
                refund.Reference,
                $"{siteUrl}/my-bookings"));

            await emailSender.SendAsync(new EmailMessage(customer.Email, customer.Name,
                $"Refund processed · GO-{refund.BookingId}", html, text), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Refund-completed email failed for refund {RefundId} — refund is settled regardless.", refund.BookingRefundId);
        }
    }
}
