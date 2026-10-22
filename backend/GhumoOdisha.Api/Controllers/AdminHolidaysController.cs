using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Holidays;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/holidays")]
[Authorize(Roles = "Admin")]
public class AdminHolidaysController(IHolidayService holidayService) : ControllerBase
{
    // ---------- Destinations ----------

    [HttpGet("destinations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminHolidayDestinationDto>>>> GetDestinations(CancellationToken cancellationToken)
    {
        var result = await holidayService.GetAdminDestinationsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminHolidayDestinationDto>>.Ok(result));
    }

    [HttpPost("destinations")]
    public async Task<ActionResult<ApiResponse<object>>> CreateDestination(SaveHolidayDestinationRequest request, CancellationToken cancellationToken)
    {
        var id = await holidayService.CreateDestinationAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { holidayDestinationId = id }, "Holiday destination added."));
    }

    [HttpPut("destinations/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDestination(int id, SaveHolidayDestinationRequest request, CancellationToken cancellationToken)
    {
        await holidayService.UpdateDestinationAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Holiday destination saved."));
    }

    [HttpDelete("destinations/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDestination(int id, CancellationToken cancellationToken)
    {
        await holidayService.DeleteDestinationAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Holiday destination deleted."));
    }

    [HttpPost("destinations/{id:int}/photo")]
    public async Task<ActionResult<ApiResponse<object>>> SetDestinationPhoto(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        await holidayService.SetDestinationPhotoAsync(id, ToImage(file), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Photo updated."));
    }

    // ---------- Packages ----------

    [HttpGet("packages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminHolidayPackageDto>>>> GetPackages(CancellationToken cancellationToken)
    {
        var result = await holidayService.GetAdminPackagesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminHolidayPackageDto>>.Ok(result));
    }

    [HttpPost("packages")]
    public async Task<ActionResult<ApiResponse<object>>> CreatePackage(SaveHolidayPackageRequest request, CancellationToken cancellationToken)
    {
        var id = await holidayService.CreatePackageAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { holidayPackageId = id }, "Holiday package added."));
    }

    [HttpPut("packages/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePackage(int id, SaveHolidayPackageRequest request, CancellationToken cancellationToken)
    {
        await holidayService.UpdatePackageAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Holiday package saved."));
    }

    [HttpDelete("packages/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePackage(int id, CancellationToken cancellationToken)
    {
        await holidayService.DeletePackageAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Holiday package deleted."));
    }

    [HttpPost("packages/{id:int}/photo")]
    public async Task<ActionResult<ApiResponse<object>>> SetPackagePhoto(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        await holidayService.SetPackagePhotoAsync(id, ToImage(file), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Photo updated."));
    }

    private static UploadedImage ToImage(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        return new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
    }
}
