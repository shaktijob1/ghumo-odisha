using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars.Dtos;

public record DriverAuthResponse(
    string Token,
    string RefreshToken,
    int DriverId,
    string Name,
    string? PhoneNumber,
    string? Email,
    DriverStatus Status);

/// <summary>The driver's own profile. <see cref="MissingForReview"/> lists what's still needed before "Submit for review".</summary>
public record DriverProfileDto(
    int DriverId,
    string Name,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? DrivingLicenceNumber,
    DateOnly? LicenceExpiryDate,
    int? ExperienceYears,
    string? ProfilePhotoUrl,
    DriverStatus Status,
    string? StatusReason,
    DateTime? SubmittedForReviewAt,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    IReadOnlyList<string> MissingForReview,
    IReadOnlyList<DriverDocumentDto> Documents);

public record UpdateDriverProfileRequest(
    string Name,
    string? Email,
    string? Address,
    string? City,
    string? DrivingLicenceNumber,
    DateOnly? LicenceExpiryDate,
    int? ExperienceYears);

/// <summary>A verification document. There's no file URL here — files are fetched through the authorized download endpoint.</summary>
public record DriverDocumentDto(int DriverDocumentId, DriverDocumentType DocumentType, int? CarId, string ContentType, DateTime CreatedAt);

public record DocumentFile(Stream Content, string ContentType, string FileName);
