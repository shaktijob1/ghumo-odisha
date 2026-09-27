using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.SearchLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

/// <summary>
/// Home-page trip search tracking. Anyone can record a search (signed-in customers are linked
/// by their token, never by a body field); rate-limited per IP so the log can't be flooded.
/// </summary>
[ApiController]
[Route("api/search-logs")]
[AllowAnonymous]
[EnableRateLimiting("SearchLog")]
public class SearchLogsController(ISearchLogService searchLogs) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Record(RecordSearchRequest request, CancellationToken cancellationToken)
    {
        int? customerId = User.IsInRole("Customer") && int.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
        await searchLogs.RecordAsync(request, customerId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Search recorded."));
    }
}
