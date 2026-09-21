using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminRoomPhotosController(ITripService tripService) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/room-photos")]
    public async Task<ActionResult<ApiResponse<object>>> AddRoomPhoto(int tripId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        var photoId = await tripService.AddRoomPhotoAsync(tripId, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { roomPhotoId = photoId }, "Room photo added."));
    }

    [HttpDelete("api/admin/room-photos/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRoomPhoto(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteRoomPhotoAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Room photo deleted."));
    }

    [HttpPut("api/admin/room-photos/{id:int}/order")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRoomPhotoOrder(int id, UpdateDisplayOrderRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateRoomPhotoOrderAsync(id, request.DisplayOrder, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Room photo order updated."));
    }
}
