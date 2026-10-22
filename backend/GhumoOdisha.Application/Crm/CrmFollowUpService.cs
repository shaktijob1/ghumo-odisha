using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Crm;

public interface ICrmFollowUpService
{
    Task<PagedResult<CrmFollowUpDto>> GetFollowUpsAsync(CrmFollowUpFilter filter, CancellationToken cancellationToken = default);
    Task<CrmFollowUpSummaryDto> GetSummaryAsync(int? assignedAdminUserId, CancellationToken cancellationToken = default);
    Task<int> CreateFollowUpAsync(CreateFollowUpRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task CompleteAsync(int followUpId, CompleteFollowUpRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task RescheduleAsync(int followUpId, RescheduleFollowUpRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(int followUpId, CancellationToken cancellationToken = default);
}

/// <summary>Follow-ups by India calendar day: today, overdue (before today) and upcoming (the next few days).</summary>
public class CrmFollowUpService(IGhumoOdishaDbContext db, IOptions<CrmOptions> options) : ICrmFollowUpService
{
    private readonly CrmOptions _options = options.Value;

    public async Task<PagedResult<CrmFollowUpDto>> GetFollowUpsAsync(CrmFollowUpFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var today = TripCalendar.Today();
        var upcomingUntil = today.AddDays(_options.UpcomingFollowUpDays);

        var query = db.CrmFollowUps.AsNoTracking();
        query = filter.Bucket switch
        {
            CrmFollowUpBucket.Today => query.Where(f => f.Status == CrmFollowUpStatus.Pending && f.DueDate == today),
            CrmFollowUpBucket.Overdue => query.Where(f => f.Status == CrmFollowUpStatus.Pending && f.DueDate < today),
            CrmFollowUpBucket.Upcoming => query.Where(f => f.Status == CrmFollowUpStatus.Pending && f.DueDate > today && f.DueDate <= upcomingUntil),
            CrmFollowUpBucket.Pending => query.Where(f => f.Status == CrmFollowUpStatus.Pending),
            CrmFollowUpBucket.Done => query.Where(f => f.Status == CrmFollowUpStatus.Done),
            _ => throw new ValidationAppException(["Unknown follow-up list."])
        };

        if (filter.AssignedAdminUserId.HasValue)
        {
            query = query.Where(f => f.AssignedAdminUserId == filter.AssignedAdminUserId.Value);
        }

        if (filter.TripId.HasValue)
        {
            query = query.Where(f => f.TripInterest != null && f.TripInterest.TripId == filter.TripId.Value);
        }

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(f => f.CustomerId == filter.CustomerId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var ordered = filter.Bucket == CrmFollowUpBucket.Done
            ? query.OrderByDescending(f => f.CompletedAt)
            // Hot leads first within a day, so the most likely bookings are called first.
            : query.OrderBy(f => f.DueDate).ThenBy(f => f.Customer.CrmStatus == CrmStatus.HotLead ? 0 : 1).ThenBy(f => f.CrmFollowUpId);

        var items = await CrmQueries.FollowUpDtos(ordered.Skip((page - 1) * pageSize).Take(pageSize), today)
            .ToListAsync(cancellationToken);

        return new PagedResult<CrmFollowUpDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<CrmFollowUpSummaryDto> GetSummaryAsync(int? assignedAdminUserId, CancellationToken cancellationToken = default)
    {
        var today = TripCalendar.Today();
        var upcomingUntil = today.AddDays(_options.UpcomingFollowUpDays);
        var pending = db.CrmFollowUps.AsNoTracking().Where(f => f.Status == CrmFollowUpStatus.Pending);
        if (assignedAdminUserId.HasValue)
        {
            pending = pending.Where(f => f.AssignedAdminUserId == assignedAdminUserId.Value);
        }

        var counts = await pending
            .Where(f => f.DueDate <= upcomingUntil)
            .GroupBy(f => f.DueDate < today ? 0 : f.DueDate == today ? 1 : 2)
            .Select(g => new { Bucket = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Bucket, x => x.Count, cancellationToken);

        return new CrmFollowUpSummaryDto(counts.GetValueOrDefault(1), counts.GetValueOrDefault(0), counts.GetValueOrDefault(2));
    }

    public async Task<int> CreateFollowUpAsync(CreateFollowUpRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking().Where(c => c.CustomerId == request.CustomerId)
            .Select(c => new { c.CustomerId, c.AssignedAdminUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Customer not found.");
        await CrmQueries.EnsureInterestBelongsAsync(db, request.CustomerId, request.CustomerTripInterestId, cancellationToken);
        await EnsureAdminAsync(request.AssignedAdminUserId, cancellationToken);

        var now = DateTime.UtcNow;
        var followUp = new CrmFollowUp
        {
            CustomerId = customer.CustomerId,
            CustomerTripInterestId = request.CustomerTripInterestId,
            DueDate = request.DueDate,
            Note = CrmHistory.Trim(request.Note, 1000),
            Status = CrmFollowUpStatus.Pending,
            AssignedAdminUserId = request.AssignedAdminUserId ?? customer.AssignedAdminUserId ?? adminUserId,
            CreatedByAdminUserId = adminUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CrmFollowUps.Add(followUp);
        await db.SaveChangesAsync(cancellationToken);
        return followUp.CrmFollowUpId;
    }

    public async Task CompleteAsync(int followUpId, CompleteFollowUpRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var followUp = await LoadPendingAsync(followUpId, cancellationToken);
        var now = DateTime.UtcNow;

        followUp.Status = CrmFollowUpStatus.Done;
        followUp.Outcome = CrmHistory.Trim(request.Outcome, 1000);
        followUp.CompletedAt = now;
        followUp.CompletedByAdminUserId = adminUserId;
        followUp.UpdatedAt = now;

        var customer = await db.Customers.FirstAsync(c => c.CustomerId == followUp.CustomerId, cancellationToken);
        customer.LastContactAt = now;
        CustomerTripInterest? interest = null;
        if (followUp.CustomerTripInterestId is { } interestId)
        {
            interest = await db.CustomerTripInterests.FirstAsync(i => i.CustomerTripInterestId == interestId, cancellationToken);
            interest.LastContactAt = now;
        }

        if (request.NewStatus is { } status)
        {
            if (status is CrmStatus.Booked or CrmStatus.Completed)
            {
                throw new ConflictException("A lead becomes Booked automatically once its booking is confirmed — use Convert to booking.");
            }

            if (interest is not null)
            {
                CrmHistory.SetInterestStatus(db, customer, interest, status, followUp.Outcome, adminUserId, now);
            }
            else
            {
                CrmHistory.SetCustomerStatus(db, customer, status, followUp.Outcome, adminUserId, now);
            }
        }

        if (request.NextDueDate is { } next)
        {
            db.CrmFollowUps.Add(new CrmFollowUp
            {
                CustomerId = followUp.CustomerId,
                CustomerTripInterestId = followUp.CustomerTripInterestId,
                DueDate = next,
                Note = CrmHistory.Trim(request.NextNote, 1000),
                Status = CrmFollowUpStatus.Pending,
                AssignedAdminUserId = followUp.AssignedAdminUserId,
                CreatedByAdminUserId = adminUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RescheduleAsync(int followUpId, RescheduleFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        var followUp = await LoadPendingAsync(followUpId, cancellationToken);
        followUp.DueDate = request.DueDate;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            followUp.Note = CrmHistory.Trim(request.Note, 1000);
        }

        followUp.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(int followUpId, CancellationToken cancellationToken = default)
    {
        var followUp = await LoadPendingAsync(followUpId, cancellationToken);
        followUp.Status = CrmFollowUpStatus.Cancelled;
        followUp.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CrmFollowUp> LoadPendingAsync(int followUpId, CancellationToken cancellationToken)
    {
        var followUp = await db.CrmFollowUps.FirstOrDefaultAsync(f => f.CrmFollowUpId == followUpId, cancellationToken)
            ?? throw new NotFoundException("Follow-up not found.");
        if (followUp.Status != CrmFollowUpStatus.Pending)
        {
            throw new ConflictException("This follow-up is already done or cancelled.");
        }

        return followUp;
    }

    private async Task EnsureAdminAsync(int? adminUserId, CancellationToken cancellationToken)
    {
        if (adminUserId is { } id && !await db.AdminUsers.AnyAsync(a => a.AdminUserId == id && a.Role == "Admin", cancellationToken))
        {
            throw new ValidationAppException(["Choose an admin user to assign this follow-up to."]);
        }
    }
}
