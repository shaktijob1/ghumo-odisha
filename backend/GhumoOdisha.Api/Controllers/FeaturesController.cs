using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

public record FeaturesDto(bool HideCarsAndHolidays);

/// <summary>Public site switches (appsettings "Features") the Angular app reads on start-up.</summary>
[ApiController]
[Route("api/features")]
public class FeaturesController(IOptions<FeatureOptions> options) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<FeaturesDto>> Get() =>
        Ok(ApiResponse<FeaturesDto>.Ok(new FeaturesDto(options.Value.HideCarsAndHolidays)));
}
