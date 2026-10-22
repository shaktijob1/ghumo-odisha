using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>
/// Admin → CRM: WhatsApp templates, manual WhatsApp Web messages and the communication log.
/// No message is ever sent from the server — the admin presses Send in WhatsApp themselves.
/// </summary>
[ApiController]
[Route("api/admin/crm")]
[Authorize(Roles = "Admin")]
public class AdminCrmMessagingController(ICrmTemplateService templates, ICrmMessageService messages) : ControllerBase
{
    // ---------- Templates ----------

    [HttpGet("whatsapp-templates")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CrmTemplateDto>>>> GetTemplates(
        [FromQuery] bool activeOnly = false, [FromQuery] CrmTemplateCategory? category = null, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<IReadOnlyList<CrmTemplateDto>>.Ok(await templates.GetTemplatesAsync(activeOnly, category, cancellationToken)));

    /// <summary>The placeholders a template may use.</summary>
    [HttpGet("whatsapp-templates/placeholders")]
    public ActionResult<ApiResponse<IReadOnlyList<string>>> GetPlaceholders() =>
        Ok(ApiResponse<IReadOnlyList<string>>.Ok(CrmMessageRenderer.Placeholders));

    [HttpGet("whatsapp-templates/{id:int}")]
    public async Task<ActionResult<ApiResponse<CrmTemplateDto>>> GetTemplate(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmTemplateDto>.Ok(await templates.GetTemplateAsync(id, cancellationToken)));

    [HttpPost("whatsapp-templates")]
    public async Task<ActionResult<ApiResponse<CrmTemplateDto>>> CreateTemplate(SaveCrmTemplateRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmTemplateDto>.Ok(await templates.CreateTemplateAsync(request, cancellationToken), "Template created."));

    [HttpPut("whatsapp-templates/{id:int}")]
    public async Task<ActionResult<ApiResponse<CrmTemplateDto>>> UpdateTemplate(int id, SaveCrmTemplateRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmTemplateDto>.Ok(await templates.UpdateTemplateAsync(id, request, cancellationToken), "Template saved."));

    [HttpPut("whatsapp-templates/{id:int}/active")]
    public async Task<ActionResult<ApiResponse<CrmTemplateDto>>> SetActive(int id, [FromQuery] bool isActive, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmTemplateDto>.Ok(await templates.SetActiveAsync(id, isActive, cancellationToken), isActive ? "Template activated." : "Template deactivated."));

    [HttpPost("whatsapp-templates/{id:int}/duplicate")]
    public async Task<ActionResult<ApiResponse<CrmTemplateDto>>> Duplicate(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmTemplateDto>.Ok(await templates.DuplicateTemplateAsync(id, cancellationToken), "Template copied."));

    [HttpDelete("whatsapp-templates/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(int id, CancellationToken cancellationToken)
    {
        await templates.DeleteTemplateAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Template deleted."));
    }

    // ---------- Messages ----------

    /// <summary>Preview: the template filled in for this customer (and trip), not sent anywhere.</summary>
    [HttpPost("messages/preview")]
    public async Task<ActionResult<ApiResponse<CrmRenderedMessageDto>>> Preview(RenderCrmMessageRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmRenderedMessageDto>.Ok(await messages.RenderAsync(request, cancellationToken)));

    /// <summary>Records "WhatsApp opened" and returns the wa.me / WhatsApp Web links for the browser to open.</summary>
    [HttpPost("messages/whatsapp")]
    public async Task<ActionResult<ApiResponse<CrmWhatsAppLinkDto>>> OpenWhatsApp(OpenWhatsAppRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmWhatsAppLinkDto>.Ok(await messages.OpenWhatsAppAsync(request, User.GetAdminId(), cancellationToken)));

    [HttpPost("messages/{id:int}/mark-sent")]
    public async Task<ActionResult<ApiResponse<object>>> MarkSent(int id, CancellationToken cancellationToken)
    {
        await messages.MarkSentAsync(id, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Marked as sent."));
    }

    [HttpPost("messages/{id:int}/mark-not-sent")]
    public async Task<ActionResult<ApiResponse<object>>> MarkNotSent(int id, CancellationToken cancellationToken)
    {
        await messages.MarkNotSentAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Marked as not sent."));
    }

    /// <summary>Record a call, email or other contact.</summary>
    [HttpPost("messages/log")]
    public async Task<ActionResult<ApiResponse<object>>> Log(LogCommunicationRequest request, CancellationToken cancellationToken)
    {
        var crmCommunicationId = await messages.LogAsync(request, User.GetAdminId(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { crmCommunicationId }, "Contact recorded."));
    }

    [HttpGet("messages")]
    public async Task<ActionResult<ApiResponse<PagedResult<CrmCommunicationDto>>>> GetHistory([FromQuery] CrmCommunicationFilter filter, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<CrmCommunicationDto>>.Ok(await messages.GetHistoryAsync(filter, cancellationToken)));
}
