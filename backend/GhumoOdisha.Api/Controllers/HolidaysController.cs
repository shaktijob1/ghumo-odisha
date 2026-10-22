using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Holidays;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>The home page's Holidays tab: destinations and custom holiday packages (enquiry only).</summary>
[ApiController]
[Route("api/holidays")]
public class HolidaysController(IHolidayService holidayService) : ControllerBase
{
    [HttpGet("destinations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HolidayDestinationDto>>>> GetDestinations(CancellationToken cancellationToken)
    {
        var result = await holidayService.GetDestinationsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HolidayDestinationDto>>.Ok(result));
    }

    [HttpGet("packages")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HolidayPackageDto>>>> GetPackages([FromQuery] int? destinationId, CancellationToken cancellationToken)
    {
        var result = await holidayService.GetPackagesAsync(destinationId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HolidayPackageDto>>.Ok(result));
    }
}
