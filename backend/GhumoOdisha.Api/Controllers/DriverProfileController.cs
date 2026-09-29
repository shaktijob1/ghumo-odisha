using GhumoOdisha.Api.Auth;
using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GhumoOdisha.Api.Controllers;

/// <summary>The signed-in driver's own profile and documents. The driver id always comes from the JWT.</summary>
[ApiController]
[Authorize(Roles = "Driver")]
[Route("api/driver")]
public class DriverProfileController(IDriverService driverService, IDriverAuthService driverAuthService) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<DriverProfileDto>>> Get(CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverProfileDto>.Ok(await driverService.GetProfileAsync(User.GetDriverId(), cancellationToken)));

    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<DriverProfileDto>>> Update(UpdateDriverProfileRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverProfileDto>.Ok(await driverService.UpdateProfileAsync(User.GetDriverId(), request, cancellationToken), "Profile saved."));

    [HttpPost("profile/photo")]
    public async Task<ActionResult<ApiResponse<DriverProfileDto>>> SetPhoto(IFormFile? file, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverProfileDto>.Ok(await driverService.SetProfilePhotoAsync(User.GetDriverId(), ToUpload(file), cancellationToken), "Photo updated."));

    [HttpPost("profile/submit")]
    public async Task<ActionResult<ApiResponse<DriverProfileDto>>> Submit(CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverProfileDto>.Ok(await driverService.SubmitForReviewAsync(User.GetDriverId(), cancellationToken), "Sent for review."));

    [HttpPost("profile/phone/request-otp")]
    [EnableRateLimiting("AuthIp")]
    public async Task<ActionResult<ApiResponse<RequestOtpResponse>>> RequestPhoneOtp(AddPhoneRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<RequestOtpResponse>.Ok(await driverAuthService.RequestAddPhoneOtpAsync(User.GetDriverId(), request, cancellationToken), "OTP sent."));

    [HttpPost("profile/phone/verify")]
    [EnableRateLimiting("AuthIp")]
    public async Task<ActionResult<ApiResponse<DriverProfileDto>>> VerifyPhone(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var driverId = User.GetDriverId();
        await driverAuthService.VerifyAddPhoneOtpAsync(driverId, request, cancellationToken);
        return Ok(ApiResponse<DriverProfileDto>.Ok(await driverService.GetProfileAsync(driverId, cancellationToken), "WhatsApp number verified."));
    }

    [HttpPost("documents")]
    public async Task<ActionResult<ApiResponse<DriverDocumentDto>>> UploadDocument(
        IFormFile? file, [FromForm] DriverDocumentType documentType, [FromForm] int? carId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DriverDocumentDto>.Ok(
            await driverService.UploadDocumentAsync(User.GetDriverId(), documentType, carId, ToUpload(file), cancellationToken), "Document uploaded."));

    [HttpGet("documents/{id:int}/file")]
    public async Task<IActionResult> DownloadDocument(int id, CancellationToken cancellationToken)
    {
        var file = await driverService.OpenDocumentAsync(User.GetDriverId(), id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("documents/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(int id, CancellationToken cancellationToken)
    {
        await driverService.DeleteDocumentAsync(User.GetDriverId(), id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Document removed."));
    }

    internal static UploadedImage ToUpload(IFormFile? file) =>
        file is null || file.Length == 0
            ? throw new ValidationAppException(["Choose a file to upload."])
            : new UploadedImage(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}
