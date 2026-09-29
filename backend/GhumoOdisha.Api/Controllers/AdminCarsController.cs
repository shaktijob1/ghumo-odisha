using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin review of cars and pricing, plus the Cars dashboard figures.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/cars")]
public class AdminCarsController(IAdminCarService adminCarService, IDriverCarService carService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<CarAdminDashboardDto>>> Dashboard(CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarAdminDashboardDto>.Ok(await adminCarService.GetDashboardAsync(cancellationToken)));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminCarListItemDto>>>> List(
        [FromQuery] AdminCarFilter filter = AdminCarFilter.All, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<AdminCarListItemDto>>.Ok(await adminCarService.ListCarsAsync(filter, search, page, pageSize, cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.GetCarAsync(id, cancellationToken)));

    [HttpPost("{id:int}/photos")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> AddPhoto(int id, IFormFile? file, [FromForm] CarPhotoKind kind, CancellationToken cancellationToken)
    {
        await carService.AdminAddPhotoAsync(User.GetAdminId(), id, kind, DriverProfileController.ToUpload(file), cancellationToken);
        return Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.GetCarAsync(id, cancellationToken), "Photo added."));
    }

    [HttpDelete("{id:int}/photos/{photoId:int}")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> DeletePhoto(int id, int photoId, CancellationToken cancellationToken)
    {
        await carService.AdminDeletePhotoAsync(User.GetAdminId(), id, photoId, cancellationToken);
        return Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.GetCarAsync(id, cancellationToken), "Photo removed."));
    }

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> Approve(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.ApproveCarAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Car approved."));

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> Reject(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.RejectCarAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Car rejected."));

    [HttpPost("{id:int}/suspend")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> Suspend(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.SuspendCarAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Car suspended."));

    [HttpPost("{id:int}/deactivate")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> Deactivate(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.DeactivateCarAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Car deactivated."));

    /// <summary>Admin edits pricing: saved as a new approved version, active immediately (old versions kept).</summary>
    [HttpPut("{id:int}/pricing")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> SetPricing(int id, SubmitPricingRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.SetPricingAsync(User.GetAdminId(), id, request, cancellationToken), "Pricing updated and active."));

    [HttpGet("pricing/pending")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminPendingPricingDto>>>> PendingPricing(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<AdminPendingPricingDto>>.Ok(await adminCarService.ListPendingPricingAsync(cancellationToken)));

    [HttpPost("pricing/{pricingId:int}/approve")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> ApprovePricing(int pricingId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.ApprovePricingAsync(User.GetAdminId(), pricingId, cancellationToken), "Pricing approved."));

    [HttpPost("pricing/{pricingId:int}/reject")]
    public async Task<ActionResult<ApiResponse<AdminCarDetailDto>>> RejectPricing(int pricingId, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminCarDetailDto>.Ok(await adminCarService.RejectPricingAsync(User.GetAdminId(), pricingId, request.Reason, cancellationToken), "Pricing rejected."));
}
