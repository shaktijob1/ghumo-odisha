using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>The signed-in driver's own cars, photos and pricing proposals. Another driver's car is a 404.</summary>
[ApiController]
[Authorize(Roles = "Driver")]
[Route("api/driver/cars")]
public class DriverCarsController(IDriverCarService carService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DriverCarDto>>>> List(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<DriverCarDto>>.Ok(await carService.ListAsync(User.GetDriverId(), cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> Get(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.GetAsync(User.GetDriverId(), id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> Create(SaveCarRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.CreateAsync(User.GetDriverId(), request, cancellationToken), "Car added."));

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> Update(int id, SaveCarRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.UpdateAsync(User.GetDriverId(), id, request, cancellationToken), "Car saved."));

    [HttpPost("{id:int}/photos")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> AddPhoto(int id, IFormFile? file, [FromForm] CarPhotoKind kind, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(
            await carService.AddPhotoAsync(User.GetDriverId(), id, kind, DriverProfileController.ToUpload(file), cancellationToken), "Photo added."));

    [HttpDelete("{id:int}/photos/{photoId:int}")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> DeletePhoto(int id, int photoId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.DeletePhotoAsync(User.GetDriverId(), id, photoId, cancellationToken), "Photo removed."));

    [HttpPost("{id:int}/submit")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> Submit(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.SubmitForReviewAsync(User.GetDriverId(), id, cancellationToken), "Sent for review."));

    [HttpPost("{id:int}/deactivate")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> Deactivate(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.DeactivateAsync(User.GetDriverId(), id, cancellationToken), "Car taken off the site."));

    [HttpGet("{id:int}/pricing")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CarPricingDto>>>> PricingHistory(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CarPricingDto>>.Ok(await carService.GetPricingHistoryAsync(User.GetDriverId(), id, cancellationToken)));

    [HttpPost("{id:int}/pricing")]
    public async Task<ActionResult<ApiResponse<DriverCarDto>>> SubmitPricing(int id, SubmitPricingRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverCarDto>.Ok(await carService.SubmitPricingAsync(User.GetDriverId(), id, request, cancellationToken),
            "Pricing sent for approval. Your current approved pricing stays active until then."));
}
