using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

public interface ICrmTripInterestService
{
    Task<PagedResult<CrmTripInterestDto>> GetInterestsAsync(CrmTripInterestFilter filter, CancellationToken cancellationToken = default);
    Task<CrmTripInterestDto> GetInterestAsync(int interestId, CancellationToken cancellationToken = default);
    Task<int> CreateInterestAsync(CreateTripInterestRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task UpdateInterestAsync(int interestId, UpdateTripInterestRequest request, CancellationToken cancellationToken = default);
    Task ChangeStatusAsync(int interestId, ChangeCrmStatusRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task DeleteInterestAsync(int interestId, CancellationToken cancellationToken = default);
    Task<CrmPipelineDto> GetPipelineAsync(int? tripId, int? tripDateSlotId, LeadSource? source, int itemsPerColumn, CancellationToken cancellationToken = default);
}

public class CrmTripInterestService(IGhumoOdishaDbContext db) : ICrmTripInterestService
{
    /// <summary>The Kanban columns, in pipeline order.</summary>
    public static readonly CrmStatus[] PipelineStages =
        [CrmStatus.NewLead, CrmStatus.Interested, CrmStatus.HotLead, CrmStatus.BookingPending, CrmStatus.Booked, CrmStatus.Completed];

    public async Task<PagedResult<CrmTripInterestDto>> GetInterestsAsync(CrmTripInterestFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var query = Filter(db.CustomerTripInterests.AsNoTracking(), filter.TripId, filter.TripDateSlotId, filter.Source);

        if (filter.Status.HasValue)
        {
            query = query.Where(i => i.Status == filter.Status.Value);
        }

        if (CrmPeriod.FromMonth(filter.Year, filter.Month) is { } period)
        {
            query = query.Where(i => i.InterestedAt >= period.FromUtc && i.InterestedAt < period.ToUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            var digits = new string(search.Where(char.IsDigit).ToArray());
            query = query.Where(i => i.Customer.Name.Contains(search)
                || (digits.Length >= 4 && i.Customer.PhoneNumber != null && i.Customer.PhoneNumber.Contains(digits))
                || i.Trip.Title.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await CrmQueries.InterestDtos(query
                .OrderBy(i => i.Status)
                .ThenByDescending(i => i.LastContactAt ?? i.InterestedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CrmTripInterestDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<CrmTripInterestDto> GetInterestAsync(int interestId, CancellationToken cancellationToken = default) =>
        await CrmQueries.InterestDtos(db.CustomerTripInterests.AsNoTracking().Where(i => i.CustomerTripInterestId == interestId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Trip interest not found.");

    public async Task<int> CreateInterestAsync(CreateTripInterestRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");
        await CrmQueries.EnsureTripAndSlotAsync(db, request.TripId, request.TripDateSlotId, cancellationToken);

        var now = DateTime.UtcNow;
        var interest = new CustomerTripInterest
        {
            CustomerId = customer.CustomerId,
            TripId = request.TripId,
            TripDateSlotId = request.TripDateSlotId,
            Travellers = request.Travellers,
            Status = CrmStatus.NewLead,
            InterestedAt = now,
            Notes = CrmHistory.Trim(request.Notes, 2000),
            ExpectedAmount = request.ExpectedAmount,
            CreatedByAdminUserId = adminUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CustomerTripInterests.Add(interest);
        if (request.Status == CrmStatus.NewLead)
        {
            // No stage change to record on the interest itself, but the history still shows when it started.
            db.CrmStatusChanges.Add(new CrmStatusChange { CustomerId = customer.CustomerId, TripInterest = interest, ToStatus = CrmStatus.NewLead, Reason = "Trip interest added", AdminUserId = adminUserId, CreatedAt = now });
        }

        CrmHistory.SetInterestStatus(db, customer, interest, request.Status, "Trip interest added", adminUserId, now);

        if (request.NextFollowUpDate is { } due)
        {
            db.CrmFollowUps.Add(new CrmFollowUp
            {
                CustomerId = customer.CustomerId,
                TripInterest = interest,
                DueDate = due,
                Status = CrmFollowUpStatus.Pending,
                AssignedAdminUserId = customer.AssignedAdminUserId ?? adminUserId,
                CreatedByAdminUserId = adminUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return interest.CustomerTripInterestId;
    }

    public async Task UpdateInterestAsync(int interestId, UpdateTripInterestRequest request, CancellationToken cancellationToken = default)
    {
        var interest = await db.CustomerTripInterests.FirstOrDefaultAsync(i => i.CustomerTripInterestId == interestId, cancellationToken)
            ?? throw new NotFoundException("Trip interest not found.");
        await CrmQueries.EnsureTripAndSlotAsync(db, request.TripId, request.TripDateSlotId, cancellationToken);

        // Once it's a real booking, the trip and date are the booking's — change them on the booking itself.
        if (interest.BookingId is not null && (interest.TripId != request.TripId || interest.TripDateSlotId != request.TripDateSlotId))
        {
            throw new ConflictException("This interest is already a booking, so its trip and date can't be changed here.");
        }

        interest.TripId = request.TripId;
        interest.TripDateSlotId = request.TripDateSlotId;
        interest.Travellers = request.Travellers;
        interest.Notes = CrmHistory.Trim(request.Notes, 2000);
        interest.ExpectedAmount = request.ExpectedAmount;
        interest.LostReason = CrmHistory.Trim(request.LostReason, 500);
        interest.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeStatusAsync(int interestId, ChangeCrmStatusRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var interest = await db.CustomerTripInterests.Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.CustomerTripInterestId == interestId, cancellationToken)
            ?? throw new NotFoundException("Trip interest not found.");

        // "Booked" and "Completed" mean a real booking: the CRM follows the booking, it can't claim one.
        if (request.Status is CrmStatus.Booked or CrmStatus.Completed && interest.BookingId is null)
        {
            throw new ConflictException("Create the booking first (Convert to booking) — a lead is marked Booked automatically once its booking is confirmed.");
        }

        var now = DateTime.UtcNow;
        if (request.Status is CrmStatus.NotInterested or CrmStatus.Cancelled && !string.IsNullOrWhiteSpace(request.Reason))
        {
            interest.LostReason = CrmHistory.Trim(request.Reason, 500);
        }

        CrmHistory.SetInterestStatus(db, interest.Customer, interest, request.Status, request.Reason, adminUserId, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteInterestAsync(int interestId, CancellationToken cancellationToken = default)
    {
        var interest = await db.CustomerTripInterests.FirstOrDefaultAsync(i => i.CustomerTripInterestId == interestId, cancellationToken)
            ?? throw new NotFoundException("Trip interest not found.");
        if (interest.BookingId is not null)
        {
            throw new ConflictException("This interest is linked to a booking and is kept for the customer's history.");
        }

        db.CustomerTripInterests.Remove(interest);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CrmPipelineDto> GetPipelineAsync(int? tripId, int? tripDateSlotId, LeadSource? source, int itemsPerColumn, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(itemsPerColumn, 1, 100);
        var query = Filter(db.CustomerTripInterests.AsNoTracking(), tripId, tripDateSlotId, source);
        // Completed trips are history: only the last 90 days show on the board.
        var completedSince = DateTime.UtcNow.AddDays(-90);

        var counts = await query
            .Where(i => i.Status != CrmStatus.Completed || i.UpdatedAt >= completedSince)
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var columns = new List<CrmPipelineColumnDto>();
        foreach (var stage in PipelineStages)
        {
            var stageQuery = query.Where(i => i.Status == stage);
            if (stage == CrmStatus.Completed)
            {
                stageQuery = stageQuery.Where(i => i.UpdatedAt >= completedSince);
            }

            var items = await CrmQueries.InterestDtos(stageQuery
                    .OrderByDescending(i => i.LastContactAt ?? i.InterestedAt)
                    .Take(take))
                .ToListAsync(cancellationToken);
            columns.Add(new CrmPipelineColumnDto(stage, counts.GetValueOrDefault(stage), items));
        }

        return new CrmPipelineDto(columns);
    }

    private static IQueryable<CustomerTripInterest> Filter(IQueryable<CustomerTripInterest> query, int? tripId, int? tripDateSlotId, LeadSource? source)
    {
        if (tripDateSlotId.HasValue)
        {
            query = query.Where(i => i.TripDateSlotId == tripDateSlotId.Value);
        }
        else if (tripId.HasValue)
        {
            query = query.Where(i => i.TripId == tripId.Value);
        }

        if (source.HasValue)
        {
            query = query.Where(i => i.Customer.LeadSource == source.Value);
        }

        return query;
    }
}
