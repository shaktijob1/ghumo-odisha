using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/organizer")]
[Authorize(Roles = "Admin")]
public class AdminOrganizerController(IOrganizerProfileService organizerProfileService) : ControllerBase
{
    [HttpPost("photo")]
    public async Task<ActionResult<ApiResponse<object>>> SetPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        await organizerProfileService.SetPhotoAsync(image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Organizer photo updated."));
    }
}
