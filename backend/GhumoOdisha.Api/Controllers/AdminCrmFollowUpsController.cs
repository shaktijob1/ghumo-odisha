using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin → CRM → Follow-ups: today, overdue and upcoming.</summary>
[ApiController]
[Route("api/admin/crm/follow-ups")]
[Authorize(Roles = "Admin")]
public class AdminCrmFollowUpsController(ICrmFollowUpService followUps) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CrmFollowUpDto>>>> GetFollowUps([FromQuery] CrmFollowUpFilter filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<CrmFollowUpDto>>.Ok(await followUps.GetFollowUpsAsync(filter, cancellationToken)));

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<CrmFollowUpSummaryDto>>> GetSummary([FromQuery] int? assignedAdminUserId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmFollowUpSummaryDto>.Ok(await followUps.GetSummaryAsync(assignedAdminUserId, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateFollowUp(CreateFollowUpRequest request, CancellationToken cancellationToken)
    {
        var crmFollowUpId = await followUps.CreateFollowUpAsync(request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { crmFollowUpId }, "Follow-up scheduled."));
    }

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<ApiResponse<object>>> Complete(int id, CompleteFollowUpRequest request, CancellationToken cancellationToken)
    {
        await followUps.CompleteAsync(id, request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Follow-up done."));
    }

    [HttpPut("{id:int}/reschedule")]
    public async Task<ActionResult<ApiResponse<object>>> Reschedule(int id, RescheduleFollowUpRequest request, CancellationToken cancellationToken)
    {
        await followUps.RescheduleAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Follow-up rescheduled."));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(int id, CancellationToken cancellationToken)
    {
        await followUps.CancelAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Follow-up cancelled."));
    }
}
