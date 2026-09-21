using FluentValidation;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminTripHighlightsController(
    ITripService tripService,
    IValidator<AddTripHighlightRequest> addValidator,
    IValidator<UpdateTripHighlightRequest> updateValidator) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/highlights")]
    public async Task<ActionResult<ApiResponse<object>>> AddHighlight(
        int tripId, [FromForm] string placeName, [FromForm] string description, IFormFile? photo, CancellationToken cancellationToken)
    {
        var request = new AddTripHighlightRequest(placeName, description);
        await ValidateAsync(addValidator, request, cancellationToken);

        if (photo is null || photo.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(photo.OpenReadStream(), photo.FileName, photo.ContentType, photo.Length);
        var highlightId = await tripService.AddTripHighlightAsync(tripId, request, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tripHighlightId = highlightId }, "Highlight added."));
    }

    [HttpPut("api/admin/highlights/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateHighlight(
        int id, [FromForm] string placeName, [FromForm] string description, [FromForm] int displayOrder, IFormFile? photo, CancellationToken cancellationToken)
    {
        var request = new UpdateTripHighlightRequest(placeName, description, displayOrder);
        await ValidateAsync(updateValidator, request, cancellationToken);

        UploadedImage? image = photo is { Length: > 0 }
            ? new UploadedImage(photo.OpenReadStream(), photo.FileName, photo.ContentType, photo.Length)
            : null;

        await tripService.UpdateTripHighlightAsync(id, request, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Highlight updated."));
    }

    [HttpDelete("api/admin/highlights/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteHighlight(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteTripHighlightAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Highlight deleted."));
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationAppException(result.Errors.Select(e => e.ErrorMessage));
        }
    }
}
