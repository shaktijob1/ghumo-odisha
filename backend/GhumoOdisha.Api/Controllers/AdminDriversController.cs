using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Admin review of drivers: list, profile + documents, approve / reject / suspend — plus adding a driver or a car for one.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/drivers")]
public class AdminDriversController(IAdminCarService adminCarService, IDriverService driverService, IDriverCarService carService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<AdminDriverDetailDto>>> Create(AdminCreateDriverRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminDriverDetailDto>.Ok(await adminCarService.CreateDriverAsync(User.GetAdminId(), request, cancellationToken), "Driver added."));

    /// <summary>Adds a car for this driver. Add photos and pricing next, then approve it to publish.</summary>
    [HttpPost("{id:int}/cars")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> CreateCar(int id, SaveCarRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.AdminCreateAsync(User.GetAdminId(), id, request, cancellationToken), "Car added."));

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminDriverListItemDto>>>> List(
        [FromQuery] AdminCarFilter filter = AdminCarFilter.All, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(ApiResponse<PagedResult<AdminDriverListItemDto>>.Ok(await adminCarService.ListDriversAsync(filter, search, page, pageSize, cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminDriverDetailDto>>> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminDriverDetailDto>.Ok(await adminCarService.GetDriverAsync(id, cancellationToken)));

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<ApiResponse<AdminDriverDetailDto>>> Approve(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminDriverDetailDto>.Ok(await adminCarService.ApproveDriverAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Driver approved."));

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<AdminDriverDetailDto>>> Reject(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminDriverDetailDto>.Ok(await adminCarService.RejectDriverAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Driver rejected."));

    [HttpPost("{id:int}/suspend")]
    public async Task<ActionResult<ApiResponse<AdminDriverDetailDto>>> Suspend(int id, AdminDecisionRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminDriverDetailDto>.Ok(await adminCarService.SuspendDriverAsync(User.GetAdminId(), id, request.Reason, cancellationToken), "Driver suspended."));

    /// <summary>The driver's starting point — km from here to each pickup are added to the fare.</summary>
    [HttpPut("{id:int}/base-location")]
    public async Task<ActionResult<ApiResponse<AdminDriverDetailDto>>> SetBaseLocation(int id, SetBaseLocationRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<AdminDriverDetailDto>.Ok(await adminCarService.SetDriverBaseLocationAsync(User.GetAdminId(), id, request, cancellationToken), "Starting point saved."));

    /// <summary>Any driver's verification document (licence, RC, insurance) — admins only.</summary>
    [HttpGet("documents/{documentId:int}/file")]
    public async Task<IActionResult> Document(int documentId, CancellationToken cancellationToken)
    {
        var file = await driverService.OpenDocumentAsync(null, documentId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
