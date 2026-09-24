using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Homepage;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/hero")]
public class HeroController(ISiteHeroPhotoService heroPhotoService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetHeroPhoto(CancellationToken cancellationToken)
    {
        var imageUrl = await heroPhotoService.GetPhotoUrlAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { imageUrl }));
    }
}
