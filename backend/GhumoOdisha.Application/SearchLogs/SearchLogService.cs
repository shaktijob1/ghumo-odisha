using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.SearchLogs;

/// <summary>Sent by the home page when a visitor searches upcoming trips by month and/or place.</summary>
public record RecordSearchRequest(string? Month, string? Place, int ResultCount);

public record SearchLogDto(
    long SearchLogId,
    DateTime CreatedAtUtc,
    string? Month,
    string? Place,
    int ResultCount,
    int? CustomerId,
    // Customer name + last 4 phone digits, or null for an anonymous visitor.
    string? CustomerLabel);

public record SearchCountDto(string Value, int Count);

public record SearchSummaryDto(
    int Searches30d,
    int NoResult30d,
    IReadOnlyList<SearchCountDto> TopPlaces,
    IReadOnlyList<SearchCountDto> TopMonths);

public interface ISearchLogService
{
    Task RecordAsync(RecordSearchRequest request, int? customerId, string? clientIp, CancellationToken cancellationToken = default);
    Task<SearchSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SearchLogDto>> GetSearchesAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}

public class SearchLogService(IGhumoOdishaDbContext db) : ISearchLogService
{
    public async Task RecordAsync(RecordSearchRequest request, int? customerId, string? clientIp, CancellationToken cancellationToken = default)
    {
        db.SearchLogs.Add(new SearchLog
        {
            Month = string.IsNullOrWhiteSpace(request.Month) ? null : request.Month.Trim(),
            Place = string.IsNullOrWhiteSpace(request.Place) ? null : request.Place.Trim(),
            ResultCount = request.ResultCount,
            CustomerId = customerId,
            ClientIp = clientIp,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SearchSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var recent = db.SearchLogs.AsNoTracking().Where(s => s.CreatedAtUtc >= since);

        var topPlaces = await recent
            .Where(s => s.Place != null)
            .GroupBy(s => s.Place!)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Key)
            .Take(8)
            .ToListAsync(cancellationToken);

        var topMonths = await recent
            .Where(s => s.Month != null)
            .GroupBy(s => s.Month!)
            .Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Key)
            .Take(8)
            .ToListAsync(cancellationToken);

        return new SearchSummaryDto(
            await recent.CountAsync(cancellationToken),
            await recent.CountAsync(s => s.ResultCount == 0, cancellationToken),
            topPlaces.Select(p => new SearchCountDto(p.Key, p.Count)).ToList(),
            topMonths.Select(m => new SearchCountDto(m.Key, m.Count)).ToList());
    }

    public async Task<PagedResult<SearchLogDto>> GetSearchesAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.SearchLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(l => (l.Place != null && l.Place.Contains(s)) || (l.Month != null && l.Month.Contains(s)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.SearchLogId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var customerIds = rows.Where(r => r.CustomerId.HasValue).Select(r => r.CustomerId!.Value).Distinct().ToList();
        var customers = customerIds.Count == 0 ? new Dictionary<int, string>() : await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.CustomerId))
            .ToDictionaryAsync(c => c.CustomerId,
                c => $"{(string.IsNullOrWhiteSpace(c.Name) ? "Customer" : c.Name)} · {(c.PhoneNumber == null ? (c.Email ?? "no phone") : "…" + (c.PhoneNumber.Length >= 4 ? c.PhoneNumber.Substring(c.PhoneNumber.Length - 4) : c.PhoneNumber))}",
                cancellationToken);

        var items = rows.Select(r => new SearchLogDto(r.SearchLogId, r.CreatedAtUtc, r.Month, r.Place, r.ResultCount, r.CustomerId,
            r.CustomerId is null ? null : customers.GetValueOrDefault(r.CustomerId.Value, $"Customer #{r.CustomerId}"))).ToList();

        return new PagedResult<SearchLogDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}
