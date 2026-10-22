using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin → CRM: which trips each customer is interested in, and the sales pipeline (Kanban).</summary>
[ApiController]
[Route("api/admin/crm")]
[Authorize(Roles = "Admin")]
public class AdminCrmTripInterestsController(ICrmTripInterestService interests) : ControllerBase
{
    [HttpGet("trip-interests")]
    public async Task<ActionResult<ApiResponse<PagedResult<CrmTripInterestDto>>>> GetInterests([FromQuery] CrmTripInterestFilter filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<CrmTripInterestDto>>.Ok(await interests.GetInterestsAsync(filter, cancellationToken)));

    [HttpGet("trip-interests/{id:int}")]
    public async Task<ActionResult<ApiResponse<CrmTripInterestDto>>> GetInterest(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmTripInterestDto>.Ok(await interests.GetInterestAsync(id, cancellationToken)));

    [HttpPost("trip-interests")]
    public async Task<ActionResult<ApiResponse<object>>> CreateInterest(CreateTripInterestRequest request, CancellationToken cancellationToken)
    {
        var customerTripInterestId = await interests.CreateInterestAsync(request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { customerTripInterestId }, "Trip interest added."));
    }

    [HttpPut("trip-interests/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateInterest(int id, UpdateTripInterestRequest request, CancellationToken cancellationToken)
    {
        await interests.UpdateInterestAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Trip interest updated."));
    }

    [HttpPut("trip-interests/{id:int}/status")]
    public async Task<ActionResult<ApiResponse<object>>> ChangeStatus(int id, ChangeCrmStatusRequest request, CancellationToken cancellationToken)
    {
        await interests.ChangeStatusAsync(id, request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Stage updated."));
    }

    [HttpDelete("trip-interests/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteInterest(int id, CancellationToken cancellationToken)
    {
        await interests.DeleteInterestAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Trip interest removed."));
    }

    [HttpGet("pipeline")]
    public async Task<ActionResult<ApiResponse<CrmPipelineDto>>> GetPipeline(
        [FromQuery] int? tripId, [FromQuery] int? tripDateSlotId, [FromQuery] LeadSource? source, [FromQuery] int itemsPerColumn = 30, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<CrmPipelineDto>.Ok(await interests.GetPipelineAsync(tripId, tripDateSlotId, source, itemsPerColumn, cancellationToken)));
}
