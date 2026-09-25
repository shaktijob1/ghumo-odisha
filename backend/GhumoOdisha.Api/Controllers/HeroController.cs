using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/hero")]
public class HeroController(ISiteHeroPhotoService heroPhotoService) : ControllerBase
{
    /// <summary>?page=trips for the Trips page banner; omitted = the Home page banner.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetHeroPhoto([FromQuery] SiteHeroPage page = SiteHeroPage.Home, CancellationToken cancellationToken = default)
    {
        var imageUrl = await heroPhotoService.GetPhotoUrlAsync(page, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { imageUrl }));
    }
}
