using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin → CRM → Trips / Campaigns: departures with seats, demand and early-booking prices.</summary>
[ApiController]
[Route("api/admin/crm")]
[Authorize(Roles = "Admin")]
public class AdminCrmDeparturesController(ICrmDepartureService departures, ICrmReportService reports) : ControllerBase
{
    [HttpGet("departures")]
    public async Task<ActionResult<ApiResponse<PagedResult<CrmDepartureDto>>>> GetDepartures([FromQuery] CrmDepartureFilter filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<CrmDepartureDto>>.Ok(await departures.GetDeparturesAsync(filter, cancellationToken)));

    [HttpGet("departures/{id:int}")]
    public async Task<ActionResult<ApiResponse<CrmDepartureDetailDto>>> GetDeparture(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmDepartureDetailDto>.Ok(await departures.GetDepartureAsync(id, cancellationToken)));

    /// <summary>Creates a real trip date (bookable on the website when opened for booking).</summary>
    [HttpPost("departures")]
    public async Task<ActionResult<ApiResponse<object>>> CreateDeparture(CreateCrmDepartureRequest request, CancellationToken cancellationToken)
    {
        var tripDateSlotId = await departures.CreateDepartureAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tripDateSlotId }, "Departure created."));
    }

    [HttpPut("departures/{id:int}/details")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDetails(int id, UpdateCrmDepartureDetailsRequest request, CancellationToken cancellationToken)
    {
        await departures.UpdateDetailsAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Departure updated."));
    }

    // ---------- Dashboard and reports ----------

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<CrmDashboardDto>>> GetDashboard([FromQuery] CrmDashboardFilter filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmDashboardDto>.Ok(await reports.GetDashboardAsync(filter, cancellationToken)));

    [HttpGet("reports/sources")]
    public async Task<ActionResult<ApiResponse<CrmSourceReportDto>>> GetSourceReport([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmSourceReportDto>.Ok(await reports.GetSourceReportAsync(from, to, cancellationToken)));

    [HttpGet("reports/monthly")]
    public async Task<ActionResult<ApiResponse<CrmMonthlyReportDto>>> GetMonthlyReport([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmMonthlyReportDto>.Ok(await reports.GetMonthlyReportAsync(year, month, cancellationToken)));
}
