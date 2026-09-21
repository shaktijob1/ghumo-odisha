using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
public class AdminVehiclePhotosController(ITripService tripService) : ControllerBase
{
    [HttpPost("api/admin/trips/{tripId:int}/vehicle-photos")]
    public async Task<ActionResult<ApiResponse<object>>> AddPhoto(int tripId, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        var photoId = await tripService.AddVehiclePhotoAsync(tripId, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { vehiclePhotoId = photoId }, "Vehicle photo added."));
    }

    [HttpDelete("api/admin/vehicle-photos/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhoto(int id, CancellationToken cancellationToken)
    {
        await tripService.DeleteVehiclePhotoAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Vehicle photo deleted."));
    }

    [HttpPut("api/admin/vehicle-photos/{id:int}/order")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePhotoOrder(int id, UpdateDisplayOrderRequest request, CancellationToken cancellationToken)
    {
        await tripService.UpdateVehiclePhotoOrderAsync(id, request.DisplayOrder, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Vehicle photo order updated."));
    }
}
