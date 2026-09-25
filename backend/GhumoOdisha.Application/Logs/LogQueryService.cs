using System.Text.RegularExpressions;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Logs;

public record LogListItemDto(
    long AppLogId,
    DateTime TimestampUtc,
    string Level,
    string Message,
    string? Source,
    string? RequestId,
    string? RequestMethod,
    string? RequestPath,
    int? StatusCode,
    double? ElapsedMs,
    int? UserId,
    string? UserRole,
    string? UserLabel,
    bool HasException);

public record LogDetailDto(
    LogListItemDto Log,
    string? Exception,
    string? ClientIp,
    string? PropertiesJson,
    // Every log line from the same request, oldest first — the step-by-step story.
    IReadOnlyList<LogListItemDto> RequestTrail);

public record FailingEndpointDto(string Method, string Path, int Count, DateTime LastAtUtc);

public record LogSummaryDto(
    int Requests24h,
    int FailedRequests24h,
    int Warnings24h,
    int Errors24h,
    int Errors7d,
    IReadOnlyList<FailingEndpointDto> TopFailing,
    IReadOnlyList<LogListItemDto> RecentErrors);

public record AdminActivityDto(
    long AdminActivityId,
    DateTime CreatedAtUtc,
    int AdminUserId,
    string AdminName,
    string Action,
    string Area,
    int? TargetId,
    string HttpMethod,
    string Path,
    int StatusCode,
    bool Succeeded,
    string? RequestId);

public record LogFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    // "Error", "Warning", "Information" — or null for all.
    string? Level = null,
    // Free text: message/path text, an Error ref / request id, or a phone number.
    string? Search = null,
    string? UserRole = null,
    int? UserId = null);

public interface ILogQueryService
{
    Task<LogSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<LogListItemDto>> GetLogsAsync(LogFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<LogDetailDto> GetLogAsync(long appLogId, CancellationToken cancellationToken = default);
    Task<PagedResult<AdminActivityDto>> GetAdminActivityAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}

public partial class LogQueryService(IGhumoOdishaDbContext db) : ILogQueryService
{
    private const string RequestSource = "Serilog.AspNetCore.RequestLoggingMiddleware";

    public async Task<LogSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dayAgo = now.AddHours(-24);
        var weekAgo = now.AddDays(-7);

        var logs = db.AppLogs.AsNoTracking();
        var requests24h = logs.Where(l => l.Source == RequestSource && l.TimestampUtc >= dayAgo);

        var topFailing = await logs
            .Where(l => l.Source == RequestSource && l.TimestampUtc >= weekAgo && l.StatusCode >= 500)
            .GroupBy(l => new { l.RequestMethod, l.RequestPath })
            .Select(g => new { g.Key.RequestMethod, g.Key.RequestPath, Count = g.Count(), Last = g.Max(l => l.TimestampUtc) })
            .OrderByDescending(g => g.Count).ThenByDescending(g => g.Last)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentErrors = await logs
            .Where(l => l.Level == "Error" || l.Level == "Fatal")
            .OrderByDescending(l => l.TimestampUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new LogSummaryDto(
            await requests24h.CountAsync(cancellationToken),
            await requests24h.CountAsync(l => l.StatusCode >= 400, cancellationToken),
            await logs.CountAsync(l => l.Level == "Warning" && l.TimestampUtc >= dayAgo, cancellationToken),
            await logs.CountAsync(l => (l.Level == "Error" || l.Level == "Fatal") && l.TimestampUtc >= dayAgo, cancellationToken),
            await logs.CountAsync(l => (l.Level == "Error" || l.Level == "Fatal") && l.TimestampUtc >= weekAgo, cancellationToken),
            topFailing.Select(g => new FailingEndpointDto(g.RequestMethod ?? "", g.RequestPath ?? "", g.Count, g.Last)).ToList(),
            await ToItemsAsync(recentErrors, cancellationToken));
    }

    public async Task<PagedResult<LogListItemDto>> GetLogsAsync(LogFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AppLogs.AsNoTracking();

        if (filter.FromUtc.HasValue) query = query.Where(l => l.TimestampUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(l => l.TimestampUtc < filter.ToUtc.Value);

        query = filter.Level switch
        {
            "Error" => query.Where(l => l.Level == "Error" || l.Level == "Fatal"),
            "Warning" => query.Where(l => l.Level == "Warning"),
            "Information" => query.Where(l => l.Level == "Information"),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(filter.UserRole) && filter.UserId.HasValue)
        {
            query = query.Where(l => l.UserRole == filter.UserRole && l.UserId == filter.UserId);
        }

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var digits = new string(search.Where(char.IsDigit).ToArray());
            if (RequestIdPattern().IsMatch(search))
            {
                // An "Error ref" a customer quoted.
                var id = search.ToUpperInvariant();
                query = query.Where(l => l.RequestId == id);
            }
            else if (digits.Length >= 4 && digits.Length == search.Replace(" ", "").TrimStart('+').Length)
            {
                // A phone number (or its last digits): logs of the matching customers.
                var phoneTail = digits.Length > 10 ? digits[^10..] : digits;
                var customerIds = await db.Customers.AsNoTracking()
                    .Where(c => c.PhoneNumber.EndsWith(phoneTail))
                    .Select(c => c.CustomerId)
                    .Take(50)
                    .ToListAsync(cancellationToken);
                query = query.Where(l => l.UserRole == "Customer" && l.UserId != null && customerIds.Contains(l.UserId.Value));
            }
            else
            {
                query = query.Where(l => l.Message.Contains(search) || (l.RequestPath != null && l.RequestPath.Contains(search)));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(l => l.TimestampUtc).ThenByDescending(l => l.AppLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LogListItemDto> { Items = await ToItemsAsync(rows, cancellationToken), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<LogDetailDto> GetLogAsync(long appLogId, CancellationToken cancellationToken = default)
    {
        var log = await db.AppLogs.AsNoTracking().FirstOrDefaultAsync(l => l.AppLogId == appLogId, cancellationToken)
            ?? throw new NotFoundException("Log entry not found.");

        var trail = string.IsNullOrEmpty(log.RequestId)
            ? [log]
            : await db.AppLogs.AsNoTracking()
                .Where(l => l.RequestId == log.RequestId)
                .OrderBy(l => l.TimestampUtc).ThenBy(l => l.AppLogId)
                .Take(200)
                .ToListAsync(cancellationToken);

        var items = await ToItemsAsync(trail.Append(log).DistinctBy(l => l.AppLogId).ToList(), cancellationToken);
        var self = items.Single(i => i.AppLogId == log.AppLogId);
        return new LogDetailDto(self, log.Exception, log.ClientIp, log.PropertiesJson,
            items.Where(i => trail.Any(t => t.AppLogId == i.AppLogId)).OrderBy(i => i.TimestampUtc).ThenBy(i => i.AppLogId).ToList());
    }

    public async Task<PagedResult<AdminActivityDto>> GetAdminActivityAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AdminActivities.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = int.TryParse(s, out var id)
                ? query.Where(a => a.TargetId == id)
                : query.Where(a => a.Action.Contains(s) || a.Area.Contains(s) || a.Path.Contains(s));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.AdminActivityId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var adminIds = rows.Select(r => r.AdminUserId).Distinct().ToList();
        var names = await db.AdminUsers.AsNoTracking()
            .Where(a => adminIds.Contains(a.AdminUserId))
            .ToDictionaryAsync(a => a.AdminUserId, a => a.Username, cancellationToken);

        var items = rows.Select(r => new AdminActivityDto(r.AdminActivityId, r.CreatedAtUtc, r.AdminUserId,
            names.GetValueOrDefault(r.AdminUserId, $"Admin #{r.AdminUserId}"), r.Action, r.Area, r.TargetId,
            r.HttpMethod, r.Path, r.StatusCode, r.Succeeded, r.RequestId)).ToList();

        return new PagedResult<AdminActivityDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    /// <summary>Adds "who" labels — customer name + last 4 phone digits, or the admin's username.</summary>
    private async Task<List<LogListItemDto>> ToItemsAsync(IReadOnlyList<AppLog> rows, CancellationToken cancellationToken)
    {
        var customerIds = rows.Where(r => r.UserRole == "Customer" && r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var adminIds = rows.Where(r => r.UserRole == "Admin" && r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();

        var customers = customerIds.Count == 0 ? new Dictionary<int, string>() : await db.Customers.AsNoTracking()
            .Where(c => customerIds.Contains(c.CustomerId))
            .ToDictionaryAsync(c => c.CustomerId,
                c => $"{(string.IsNullOrWhiteSpace(c.Name) ? "Customer" : c.Name)} · …{(c.PhoneNumber.Length >= 4 ? c.PhoneNumber[^4..] : c.PhoneNumber)}",
                cancellationToken);
        var admins = adminIds.Count == 0 ? new Dictionary<int, string>() : await db.AdminUsers.AsNoTracking()
            .Where(a => adminIds.Contains(a.AdminUserId))
            .ToDictionaryAsync(a => a.AdminUserId, a => $"Admin · {a.Username}", cancellationToken);

        return rows.Select(r => new LogListItemDto(
            r.AppLogId, r.TimestampUtc, r.Level, r.Message, ShortSource(r.Source), r.RequestId, r.RequestMethod, r.RequestPath,
            r.StatusCode, r.ElapsedMs, r.UserId, r.UserRole,
            r.UserId is null ? null
                : r.UserRole == "Customer" ? customers.GetValueOrDefault(r.UserId.Value, $"Customer #{r.UserId}")
                : r.UserRole == "Admin" ? admins.GetValueOrDefault(r.UserId.Value, $"Admin #{r.UserId}")
                : $"User #{r.UserId}",
            r.Exception is not null)).ToList();
    }

    /// <summary>"GhumoOdisha.Application.Payments.BookingPaymentService" → "BookingPaymentService".</summary>
    private static string? ShortSource(string? source) =>
        source is null ? null
        : source == RequestSource ? "Request"
        : source[(source.LastIndexOf('.') + 1)..];

    [GeneratedRegex("^[A-HJ-NP-Z2-9]{8}$", RegexOptions.IgnoreCase)]
    private static partial Regex RequestIdPattern();
}
