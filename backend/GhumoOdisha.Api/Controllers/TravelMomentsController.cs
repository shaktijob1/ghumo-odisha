using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Homepage;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

[ApiController]
[Route("api/travel-moments")]
public class TravelMomentsController(ITravelMomentService travelMomentService) : ControllerBase
{
    /// <summary>The home page's "Real Travel Moments" photos, in display order.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TravelMomentDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await travelMomentService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TravelMomentDto>>.Ok(result));
    }
}
