using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/admin/hero")]
[Authorize(Roles = "Admin")]
public class AdminHeroController(ISiteHeroPhotoService heroPhotoService) : ControllerBase
{
    /// <summary>?page=trips sets the Trips page banner; omitted = the Home page banner.</summary>
    [HttpPost("photo")]
    public async Task<ActionResult<ApiResponse<object>>> SetPhoto(IFormFile? file, [FromQuery] SiteHeroPage page = SiteHeroPage.Home, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(page))
        {
            throw new ValidationAppException(["Unknown hero page."]);
        }

        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        await heroPhotoService.SetPhotoAsync(page, image, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, page == SiteHeroPage.Trips ? "Trips page hero photo updated." : "Dashboard hero photo updated."));
    }
}
