using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Destinations;
using GhumoOdisha.Application.Destinations.Dtos;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/destinations")]
public class DestinationsController(IDestinationService destinationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DestinationSummaryDto>>>> GetActiveDestinations(CancellationToken cancellationToken)
    {
        var result = await destinationService.GetActiveDestinationsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DestinationSummaryDto>>.Ok(result));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<ApiResponse<DestinationDetailDto>>> GetDestination(string slug, CancellationToken cancellationToken)
    {
        var result = await destinationService.GetDestinationBySlugAsync(slug, cancellationToken);
        return Ok(ApiResponse<DestinationDetailDto>.Ok(result));
    }

    [HttpGet("{slug}/trips")]
    public async Task<ActionResult<ApiResponse<PagedResult<TripSummaryDto>>>> GetDestinationTrips(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await destinationService.GetDestinationTripsAsync(slug, page, pageSize, fromDate, toDate, cancellationToken);
        return Ok(ApiResponse<PagedResult<TripSummaryDto>>.Ok(result));
    }
}
