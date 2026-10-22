using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin → CRM: CSV import of leads, and re-syncing bookings into the CRM.</summary>
[ApiController]
[Route("api/admin/crm")]
[Authorize(Roles = "Admin")]
public class AdminCrmImportController(ICrmImportService imports, IGhumoOdishaDbContext db) : ControllerBase
{
    /// <summary>What importing these rows would do — nothing is saved.</summary>
    [HttpPost("import/preview")]
    public async Task<ActionResult<ApiResponse<CrmImportPreviewDto>>> Preview(CrmImportRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmImportPreviewDto>.Ok(await imports.PreviewAsync(request, cancellationToken)));

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<CrmImportResultDto>>> Import(CrmImportRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CrmImportResultDto>.Ok(await imports.ImportAsync(request, User.GetAdminId(), cancellationToken), "Import finished."));

    /// <summary>Adds any paid booking the CRM doesn't know about yet (normally done automatically).</summary>
    [HttpPost("sync-bookings")]
    public async Task<ActionResult<ApiResponse<object>>> SyncBookings(CancellationToken cancellationToken)
    {
        var linked = await CrmBookingSync.BackfillAsync(db, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { linked }, linked == 0 ? "Everything was already in the CRM." : $"{linked} booking(s) added to the CRM."));
    }
}
