using GhumoOdisha.Application.Collections;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Collections desk: who has paid what on each trip, and the payment QR shown to travellers.
/// Recording a collected amount uses POST api/admin/bookings/{id}/payments.</summary>
[ApiController]
[Route("api/admin/collections")]
[Authorize(Roles = "Admin")]
public class AdminCollectionsController(ICollectionService collectionService) : ControllerBase
{
    [HttpGet("trips")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CollectionTripDto>>>> GetTrips(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<CollectionTripDto>>.Ok(await collectionService.GetTripsAsync(cancellationToken)));

    [HttpGet("trips/{tripId:int}")]
    public async Task<ActionResult<ApiResponse<CollectionSheetDto>>> GetSheet(int tripId, [FromQuery] int? dateSlotId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CollectionSheetDto>.Ok(await collectionService.GetSheetAsync(tripId, dateSlotId, cancellationToken)));

    [HttpGet("qr")]
    public async Task<ActionResult<ApiResponse<PaymentQrDto?>>> GetQr(CancellationToken cancellationToken) =>
        Ok(ApiResponse<PaymentQrDto?>.Ok(await collectionService.GetPaymentQrAsync(cancellationToken)));

    [HttpPost("qr")]
    public async Task<ActionResult<ApiResponse<PaymentQrDto>>> SetQr(IFormFile? file, [FromForm] string? caption, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationAppException(["A QR code image is required."]);
        }

        var image = new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
        var qr = await collectionService.SetPaymentQrAsync(image, caption, cancellationToken);
        return Ok(ApiResponse<PaymentQrDto>.Ok(qr, "Payment QR updated."));
    }

    [HttpDelete("qr")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveQr(CancellationToken cancellationToken)
    {
        await collectionService.RemovePaymentQrAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Payment QR removed."));
    }
}
