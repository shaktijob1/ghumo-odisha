using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

public record TravelMomentCaptionRequest(string? Caption);

public record TravelMomentMoveRequest(int Direction);

[ApiController]
[Route("api/admin/travel-moments")]
[Authorize(Roles = "Admin")]
public class AdminTravelMomentsController(ITravelMomentService travelMomentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<TravelMomentDto>>> Add(IFormFile? file, [FromForm] string? caption, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A photo file is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        var result = await travelMomentService.AddAsync(image, caption, cancellationToken);
        return Ok(ApiResponse<TravelMomentDto>.Ok(result, "Photo added."));
    }

    [HttpPut("{id:int}/caption")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateCaption(int id, TravelMomentCaptionRequest request, CancellationToken cancellationToken)
    {
        await travelMomentService.UpdateCaptionAsync(id, request.Caption, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Caption saved."));
    }

    [HttpPost("{id:int}/move")]
    public async Task<ActionResult<ApiResponse<object>>> Move(int id, TravelMomentMoveRequest request, CancellationToken cancellationToken)
    {
        await travelMomentService.MoveAsync(id, request.Direction, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Order updated."));
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken cancellationToken)
    {
        await travelMomentService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Photo deleted."));
    }
}
