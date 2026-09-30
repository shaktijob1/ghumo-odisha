using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Where cars pick customers up from: drawn zones and PIN code lists.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/service-areas")]
public class AdminServiceAreasController(IServiceAreaService serviceAreas) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServiceAreaDto>>>> List(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<ServiceAreaDto>>.Ok(await serviceAreas.ListAsync(cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ServiceAreaDto>>> Create(SaveServiceAreaRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ServiceAreaDto>.Ok(await serviceAreas.CreateAsync(request, cancellationToken), "Service area added."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ServiceAreaDto>>> Update(int id, SaveServiceAreaRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ServiceAreaDto>.Ok(await serviceAreas.UpdateAsync(id, request, cancellationToken), "Service area saved."));

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken cancellationToken)
    {
        await serviceAreas.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Service area deleted."));
    }
}
