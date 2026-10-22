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
    /// <summary>The Travel Organizer card's texts as stored, plus the defaults empty fields fall back to.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<OrganizerProfileDto>>> GetProfile(CancellationToken cancellationToken)
    {
        var result = await organizerProfileService.GetProfileAsync(cancellationToken);
        return Ok(ApiResponse<OrganizerProfileDto>.Ok(result));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile(UpdateOrganizerProfileRequest request, CancellationToken cancellationToken)
    {
        await organizerProfileService.UpdateProfileAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Travel Organizer details saved."));
    }

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
