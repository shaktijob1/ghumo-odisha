using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetDashboardAsync(cancellationToken);
        return Ok(ApiResponse<DashboardDto>.Ok(result));
    }
}
