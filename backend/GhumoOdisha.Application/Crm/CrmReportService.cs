using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

public interface ICrmReportService
{
    Task<CrmDashboardDto> GetDashboardAsync(CrmDashboardFilter filter, CancellationToken cancellationToken = default);
    Task<CrmSourceReportDto> GetSourceReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<CrmMonthlyReportDto> GetMonthlyReportAsync(int year, int month, CancellationToken cancellationToken = default);
}

/// <summary>
/// CRM numbers. A "lead" is a trip interest (an enquiry about a trip), dated by when it came in;
/// bookings and money always come from the real Bookings and payments, never from CRM guesses.
/// Months are India calendar months and work for any year.
/// </summary>
public class CrmReportService(IGhumoOdishaDbContext db, ICrmFollowUpService followUps, ICrmDepartureService departures) : ICrmReportService
{
    // A List, not an array: on .NET 10 an array's Contains binds to a span overload EF Core can't translate.
    private static readonly List<BookingStatus> BookedStatuses = [BookingStatus.Confirmed, BookingStatus.Completed];

    public async Task<CrmDashboardDto> GetDashboardAsync(CrmDashboardFilter filter, CancellationToken cancellationToken = default)
    {
        var period = CrmPeriod.FromMonth(filter.Year, filter.Month);

        var interests = db.CustomerTripInterests.AsNoTracking();
        if (filter.TripDateSlotId.HasValue)
        {
            interests = interests.Where(i => i.TripDateSlotId == filter.TripDateSlotId.Value);
        }
        else if (filter.TripId.HasValue)
        {
            interests = interests.Where(i => i.TripId == filter.TripId.Value);
        }

        if (filter.Source.HasValue)
        {
            interests = interests.Where(i => i.Customer.LeadSource == filter.Source.Value);
        }

        if (filter.Status.HasValue)
        {
            interests = interests.Where(i => i.Status == filter.Status.Value);
        }

        if (period is { } p)
        {
            interests = interests.Where(i => i.InterestedAt >= p.FromUtc && i.InterestedAt < p.ToUtc);
        }

        var byStatus = await interests
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        int Count(CrmStatus status) => byStatus.GetValueOrDefault(status);
        var totalLeads = byStatus.Values.Sum();

        var customers = db.Customers.AsNoTracking();
        if (filter.Source.HasValue)
        {
            customers = customers.Where(c => c.LeadSource == filter.Source.Value);
        }

        if (filter.Status.HasValue)
        {
            customers = customers.Where(c => c.CrmStatus == filter.Status.Value);
        }

        if (filter.TripDateSlotId.HasValue)
        {
            var slotId = filter.TripDateSlotId.Value;
            customers = customers.Where(c => c.TripInterests.Any(i => i.TripDateSlotId == slotId) || c.Bookings.Any(b => b.TripDateSlotId == slotId));
        }
        else if (filter.TripId.HasValue)
        {
            var tripId = filter.TripId.Value;
            customers = customers.Where(c => c.TripInterests.Any(i => i.TripId == tripId) || c.Bookings.Any(b => b.TripId == tripId));
        }

        var totalCustomers = period is { } cp
            ? await customers.CountAsync(c => c.CreatedAt >= cp.FromUtc && c.CreatedAt < cp.ToUtc, cancellationToken)
            : await customers.CountAsync(cancellationToken);
        // Two or more trips booked (and not cancelled) — whatever stage the CRM shows.
        var repeatCustomers = await customers.CountAsync(c => c.Bookings.Count(b => BookedStatuses.Contains(b.BookingStatus)) >= 2, cancellationToken);

        var summary = await followUps.GetSummaryAsync(null, cancellationToken);
        var today = await followUps.GetFollowUpsAsync(new CrmFollowUpFilter(CrmFollowUpBucket.Today, PageSize: 10), cancellationToken);
        var upcoming = await departures.GetUpcomingAsync(5, filter.TripId, cancellationToken);

        var converted = Count(CrmStatus.Booked) + Count(CrmStatus.Completed);
        return new CrmDashboardDto(
            totalCustomers,
            totalLeads,
            Count(CrmStatus.NewLead),
            Count(CrmStatus.Interested),
            Count(CrmStatus.HotLead),
            Count(CrmStatus.FollowUp),
            Count(CrmStatus.BookingPending),
            Count(CrmStatus.Booked),
            Count(CrmStatus.Completed),
            Count(CrmStatus.Cancelled),
            Count(CrmStatus.NotInterested),
            Count(CrmStatus.Later),
            repeatCustomers,
            summary.Today,
            summary.Overdue,
            summary.Upcoming,
            Percent(converted, totalLeads),
            today.Items,
            upcoming);
    }

    public async Task<CrmSourceReportDto> GetSourceReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && to < from)
        {
            throw new ValidationAppException(["The end date is before the start date."]);
        }

        CrmPeriod? period = from.HasValue || to.HasValue
            ? CrmPeriod.Between(from ?? new DateOnly(2000, 1, 1), to ?? TripCalendar.Today())
            : null;

        return new CrmSourceReportDto(from, to, await SourceRowsAsync(period, cancellationToken));
    }

    public async Task<CrmMonthlyReportDto> GetMonthlyReportAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var period = CrmPeriod.FromMonth(year, month)!.Value;

        var leads = await db.CustomerTripInterests.AsNoTracking()
            .CountAsync(i => i.InterestedAt >= period.FromUtc && i.InterestedAt < period.ToUtc, cancellationToken);

        // Bookings confirmed in the month (whatever happened to them later).
        var confirmed = db.Bookings.AsNoTracking()
            .Where(b => b.ConfirmedAt != null && b.ConfirmedAt >= period.FromUtc && b.ConfirmedAt < period.ToUtc);
        var bookingTotals = await confirmed
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Seats = g.Sum(b => b.NumberOfSeats), Value = g.Sum(b => b.TotalAmount) })
            .FirstOrDefaultAsync(cancellationToken);

        var payments = await db.BookingPayments.AsNoTracking()
            .Where(p => p.PaidAt >= period.FromUtc && p.PaidAt < period.ToUtc)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var cancelled = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.BookingStatus == BookingStatus.Cancelled && b.ConfirmedAt != null
                          && b.CancelledAt >= period.FromUtc && b.CancelledAt < period.ToUtc, cancellationToken);

        // Trips that finished in the month.
        var firstDay = CrmPeriod.FirstDay(year, month);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var completed = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.BookingStatus == BookingStatus.Completed && b.TripDateSlot != null
                          && b.TripDateSlot.EndDate >= firstDay && b.TripDateSlot.EndDate <= lastDay, cancellationToken);

        var topTrips = await confirmed
            .GroupBy(b => new { b.TripId, b.Trip.Title })
            .Select(g => new { g.Key.TripId, g.Key.Title, Bookings = g.Count(), Seats = g.Sum(b => b.NumberOfSeats), Value = g.Sum(b => b.TotalAmount) })
            .OrderByDescending(t => t.Bookings).ThenByDescending(t => t.Value)
            .Take(5)
            .Select(t => new CrmTopTripDto(t.TripId, t.Title, t.Bookings, t.Seats, t.Value))
            .ToListAsync(cancellationToken);

        var sources = (await SourceRowsAsync(period, cancellationToken))
            .Where(r => r.Leads > 0 || r.Bookings > 0)
            .OrderByDescending(r => r.Bookings).ThenByDescending(r => r.Leads)
            .ToList();

        var bookingCount = bookingTotals?.Count ?? 0;
        return new CrmMonthlyReportDto(
            year,
            month,
            leads,
            bookingCount,
            bookingTotals?.Seats ?? 0,
            bookingTotals?.Value ?? 0m,
            payments,
            Percent(bookingCount, leads),
            cancelled,
            completed,
            topTrips,
            sources);
    }

    /// <summary>Leads (trip interests) and bookings confirmed in the period, by the customer's lead source.</summary>
    private async Task<IReadOnlyList<CrmSourceRowDto>> SourceRowsAsync(CrmPeriod? period, CancellationToken cancellationToken)
    {
        IQueryable<CustomerTripInterest> interests = db.CustomerTripInterests.AsNoTracking();
        IQueryable<Booking> bookings = db.Bookings.AsNoTracking().Where(b => b.ConfirmedAt != null);
        if (period is { } p)
        {
            interests = interests.Where(i => i.InterestedAt >= p.FromUtc && i.InterestedAt < p.ToUtc);
            bookings = bookings.Where(b => b.ConfirmedAt >= p.FromUtc && b.ConfirmedAt < p.ToUtc);
        }

        var leadCounts = await interests
            .GroupBy(i => i.Customer.LeadSource)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Source, x => x.Count, cancellationToken);
        var bookingStats = await bookings
            .GroupBy(b => b.Customer.LeadSource)
            .Select(g => new { Source = g.Key, Count = g.Count(), Value = g.Sum(b => b.TotalAmount) })
            .ToDictionaryAsync(x => x.Source, x => (x.Count, x.Value), cancellationToken);

        return Enum.GetValues<LeadSource>()
            .Select(source =>
            {
                var leads = leadCounts.GetValueOrDefault(source);
                var (count, value) = bookingStats.GetValueOrDefault(source);
                return new CrmSourceRowDto(source, leads, count, value, Percent(count, leads));
            })
            .ToList();
    }

    /// <summary>Percentage to 2 decimals; 0 when there's nothing to divide by.</summary>
    internal static decimal Percent(int part, int whole) =>
        whole == 0 ? 0m : Math.Round(part * 100m / whole, 2, MidpointRounding.AwayFromZero);
}
