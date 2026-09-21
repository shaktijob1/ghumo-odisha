using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminTripPhotosController(ITripService tripService) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/photos")]
    public async Task<ActionResult<ApiResponse<object>>> AddPhoto(int tripId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        var photoId = await tripService.AddTripPhotoAsync(tripId, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { tripPhotoId = photoId }, "Photo added."));
    }

    [HttpDelete("api/admin/trip-photos/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhoto(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteTripPhotoAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Photo deleted."));
    }

    [HttpPut("api/admin/trip-photos/{id:int}/order")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePhotoOrder(int id, UpdateDisplayOrderRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateTripPhotoOrderAsync(id, request.DisplayOrder, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Photo order updated."));
    }
}
