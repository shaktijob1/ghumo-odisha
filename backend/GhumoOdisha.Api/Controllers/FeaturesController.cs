using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Maps;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

/// <param name="GoogleMapsApiKey">Browser key for Maps JavaScript / Places (public by nature; restricted by referrer in Google Cloud). Null when not configured.</param>
/// <param name="GoogleMapsMapId">Map ID for maps with markers.</param>
public record FeaturesDto(bool HideCarsAndHolidays, string? GoogleMapsApiKey, string GoogleMapsMapId);

/// <summary>Public site switches (appsettings "Features") the Angular app reads on start-up.</summary>
[ApiController]
[Route("api/features")]
public class FeaturesController(IOptions<FeatureOptions> options, IOptions<GoogleMapsOptions> maps) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<FeaturesDto>> Get() =>
        Ok(ApiResponse<FeaturesDto>.Ok(new FeaturesDto(
            options.Value.HideCarsAndHolidays,
            string.IsNullOrWhiteSpace(maps.Value.BrowserApiKey) ? null : maps.Value.BrowserApiKey.Trim(),
            string.IsNullOrWhiteSpace(maps.Value.MapId) ? "DEMO_MAP_ID" : maps.Value.MapId.Trim())));
}
