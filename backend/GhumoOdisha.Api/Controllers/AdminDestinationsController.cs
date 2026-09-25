using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Destinations;
using GhumoOdisha.Application.Destinations.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/destinations")]
[Authorize(Roles = "Admin")]
public class AdminDestinationsController(IDestinationService destinationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminDestinationListItemDto>>>> GetDestinations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await destinationService.GetAdminDestinationsAsync(page, pageSize, search, cancellationToken);
        return Ok(ApiResponse<PagedResult<AdminDestinationListItemDto>>.Ok(result));
    }

    [HttpGet("picker")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AdminDestinationListItemDto>>>> GetAllForPicker(CancellationToken cancellationToken)
    {
        var result = await destinationService.GetAllForPickerAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AdminDestinationListItemDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateDestination(CreateDestinationRequest request, CancellationToken cancellationToken)
    {
        var destinationId = await destinationService.CreateDestinationAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { destinationId }, "Destination created."));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<AdminDestinationDetailDto>>> GetDestinationDetail(int id, CancellationToken cancellationToken)
    {
        var result = await destinationService.GetAdminDestinationDetailAsync(id, cancellationToken);
        return Ok(ApiResponse<AdminDestinationDetailDto>.Ok(result));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDestination(int id, UpdateDestinationRequest request, CancellationToken cancellationToken)
    {
        await destinationService.UpdateDestinationAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Destination updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDestination(int id, CancellationToken cancellationToken)
    {
        await destinationService.DeleteDestinationAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Destination deleted."));
    }

    [HttpPost("{id:int}/hero-image")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateHeroImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        await destinationService.UpdateDestinationHeroImageAsync(id, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Hero image updated."));
    }

    [HttpPost("{id:int}/cover-image")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCoverImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        await destinationService.UpdateDestinationCoverImageAsync(id, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Cover image updated."));
    }
}
