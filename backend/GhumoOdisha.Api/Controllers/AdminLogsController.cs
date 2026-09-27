using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Logs;
using GhumoOdisha.Application.SearchLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin Logs screen: overview, searchable log list, per-request detail, admin activity trail.</summary>
[ApiController]
[Route("api/admin/logs")]
[Authorize(Roles = "Admin")]
public class AdminLogsController(ILogQueryService logs, ISearchLogService searchLogs) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<LogSummaryDto>>> GetSummary(CancellationToken cancellationToken) =>
        Ok(ApiResponse<LogSummaryDto>.Ok(await logs.GetSummaryAsync(cancellationToken)));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LogListItemDto>>>> GetLogs(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? level = null,
        [FromQuery] string? search = null,
        [FromQuery] string? userRole = null,
        [FromQuery] int? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new LogFilter(from?.ToUniversalTime(), to?.ToUniversalTime(), level, search, userRole, userId);
        return Ok(ApiResponse<PagedResult<LogListItemDto>>.Ok(await logs.GetLogsAsync(filter, page, pageSize, cancellationToken)));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<LogDetailDto>>> GetLog(long id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<LogDetailDto>.Ok(await logs.GetLogAsync(id, cancellationToken)));

    [HttpGet("activity")]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminActivityDto>>>> GetActivity(
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<AdminActivityDto>>.Ok(await logs.GetAdminActivityAsync(search, page, pageSize, cancellationToken)));

    /// <summary>What visitors searched for on the home page (month/place) — the last 30 days at a glance.</summary>
    [HttpGet("searches/summary")]
    public async Task<ActionResult<ApiResponse<SearchSummaryDto>>> GetSearchSummary(CancellationToken cancellationToken) =>
        Ok(ApiResponse<SearchSummaryDto>.Ok(await searchLogs.GetSummaryAsync(cancellationToken)));

    [HttpGet("searches")]
    public async Task<ActionResult<ApiResponse<PagedResult<SearchLogDto>>>> GetSearches(
        [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<SearchLogDto>>.Ok(await searchLogs.GetSearchesAsync(search, page, pageSize, cancellationToken)));
}
