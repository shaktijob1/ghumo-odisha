using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Maps;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Cars;

public interface IDriverService
{
    Task<DriverProfileDto> GetProfileAsync(int driverId, CancellationToken cancellationToken = default);
    Task<DriverProfileDto> UpdateProfileAsync(int driverId, UpdateDriverProfileRequest request, CancellationToken cancellationToken = default);
    Task<DriverProfileDto> SetBaseLocationAsync(int driverId, SetBaseLocationRequest request, CancellationToken cancellationToken = default);
    Task<DriverProfileDto> SetProfilePhotoAsync(int driverId, UploadedImage image, CancellationToken cancellationToken = default);
    Task<DriverProfileDto> SubmitForReviewAsync(int driverId, CancellationToken cancellationToken = default);
    Task<DriverDocumentDto> UploadDocumentAsync(int driverId, DriverDocumentType type, int? carId, UploadedImage file, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(int driverId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>A document file. With a driverId only that driver's own documents open; null = admin access.</summary>
    Task<DocumentFile> OpenDocumentAsync(int? driverId, int documentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The driver's own profile and verification documents. Every method takes the driver id from the
/// JWT (never from the request), so a driver can only ever read or change their own account.
/// </summary>
public class DriverService(IGhumoOdishaDbContext db, IImageStorage storage) : IDriverService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private static readonly string[] DocumentContentTypes = [.. ImageContentTypes, "application/pdf"];

    public async Task<DriverProfileDto> GetProfileAsync(int driverId, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(driverId, cancellationToken));

    public async Task<DriverProfileDto> UpdateProfileAsync(int driverId, UpdateDriverProfileRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await LoadAsync(driverId, cancellationToken);
        var now = DateTime.UtcNow;

        var newLicence = request.DrivingLicenceNumber?.Trim().ToUpperInvariant();
        var licenceChanged = !string.Equals(driver.DrivingLicenceNumber, newLicence, StringComparison.Ordinal);
        var nameChanged = driver.Name != request.Name.Trim();

        if (licenceChanged || nameChanged)
        {
            CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DriverIdentityChanged", "Name / licence changed", CarActor.Driver(driverId),
                $"{driver.Name} · {driver.DrivingLicenceNumber}", $"{request.Name.Trim()} · {newLicence}");
        }

        driver.Name = request.Name.Trim();
        // A Google-confirmed email is the sign-in key — only an unconfirmed one can be edited.
        if (!driver.EmailVerified)
        {
            driver.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        }
        driver.Address = request.Address?.Trim();
        driver.City = string.IsNullOrWhiteSpace(request.City) ? null : CarRules.NormalizeCity(request.City);
        driver.DrivingLicenceNumber = newLicence;
        driver.LicenceExpiryDate = request.LicenceExpiryDate;
        driver.ExperienceYears = request.ExperienceYears;
        driver.UpdatedAt = now;

        // A verified identity that changes has to be checked again — the driver's cars leave search until then.
        if (driver.Status == DriverStatus.Approved && (licenceChanged || nameChanged))
        {
            driver.Status = DriverStatus.Pending;
            driver.SubmittedForReviewAt = now;
            CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DriverResubmitted", "Sent for review again after identity change",
                CarActor.Driver(driverId), nameof(DriverStatus.Approved), nameof(DriverStatus.Pending));
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(driver);
    }

    public async Task<DriverProfileDto> SetBaseLocationAsync(int driverId, SetBaseLocationRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await LoadAsync(driverId, cancellationToken);
        ApplyBaseLocation(db, driver, request, CarActor.Driver(driverId));
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(driver);
    }

    /// <summary>Shared by the driver's own profile and the admin driver page. Changes future quotes only — booked fares are frozen.</summary>
    internal static void ApplyBaseLocation(IGhumoOdishaDbContext db, Driver driver, SetBaseLocationRequest request, CarActor actor)
    {
        if (!new GeoPoint(request.Latitude, request.Longitude).IsValid)
        {
            throw new ValidationAppException(["Choose the starting point on the map."]);
        }

        var label = request.Label.Trim();
        CarAudit.Record(db, CarAuditEntity.Driver, driver.DriverId, "DriverBaseLocationChanged", "Starting point changed", actor,
            driver.BaseLocationLabel, label);
        driver.BaseLatitude = Math.Round(request.Latitude, 6);
        driver.BaseLongitude = Math.Round(request.Longitude, 6);
        driver.BaseLocationLabel = label.Length > 300 ? label[..300] : label;
        driver.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<DriverProfileDto> SetProfilePhotoAsync(int driverId, UploadedImage image, CancellationToken cancellationToken = default)
    {
        ValidateFile(image, ImageContentTypes, "Only JPG, PNG or WebP photos are allowed.");
        var driver = await LoadAsync(driverId, cancellationToken);

        var oldUrl = driver.ProfilePhotoUrl;
        driver.ProfilePhotoUrl = await storage.SaveAsync(image.Content, image.FileName, image.ContentType, "drivers", cancellationToken);
        driver.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (oldUrl is not null)
        {
            storage.Delete(oldUrl);
        }
        return ToDto(driver);
    }

    public async Task<DriverProfileDto> SubmitForReviewAsync(int driverId, CancellationToken cancellationToken = default)
    {
        var driver = await LoadAsync(driverId, cancellationToken);
        switch (driver.Status)
        {
            case DriverStatus.Suspended:
                throw new ConflictException("Your account is suspended. Please contact Ghumo Odisha.");
            case DriverStatus.Approved:
                throw new ConflictException("Your profile is already approved.");
            case DriverStatus.Pending when driver.SubmittedForReviewAt is not null:
                throw new ConflictException("Your profile is already waiting for review.");
        }

        var missing = CarMapping.MissingForReview(driver);
        if (missing.Count > 0)
        {
            throw new ValidationAppException(missing);
        }

        var old = driver.Status;
        driver.Status = DriverStatus.Pending;
        driver.StatusReason = null;
        driver.SubmittedForReviewAt = DateTime.UtcNow;
        driver.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DriverSubmitted", "Profile sent for review", CarActor.Driver(driverId),
            old.ToString(), nameof(DriverStatus.Pending));

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(driver);
    }

    public async Task<DriverDocumentDto> UploadDocumentAsync(int driverId, DriverDocumentType type, int? carId, UploadedImage file, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ValidationAppException(["Choose a document type."]);
        }
        ValidateFile(file, DocumentContentTypes, "Upload a JPG, PNG, WebP photo or a PDF.");

        if (carId is not null && !await db.Cars.AnyAsync(c => c.CarId == carId && c.DriverId == driverId, cancellationToken))
        {
            throw new NotFoundException("Car not found.");
        }

        var url = await storage.SaveAsync(file.Content, file.FileName, file.ContentType, "driver-documents", cancellationToken);
        var document = new DriverDocument
        {
            DriverId = driverId,
            CarId = carId,
            DocumentType = type,
            FileUrl = url,
            ContentType = file.ContentType.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        db.DriverDocuments.Add(document);
        CarAudit.Record(db, carId is null ? CarAuditEntity.Driver : CarAuditEntity.Car, carId ?? driverId, "DocumentUploaded",
            $"{type} document uploaded", CarActor.Driver(driverId));
        await db.SaveChangesAsync(cancellationToken);
        return CarMapping.ToDto(document);
    }

    public async Task DeleteDocumentAsync(int driverId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await db.DriverDocuments.Include(d => d.Driver)
            .FirstOrDefaultAsync(d => d.DriverDocumentId == documentId && d.DriverId == driverId, cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        // Verified documents stay on file — upload a newer one instead.
        if (document.Driver.Status == DriverStatus.Approved)
        {
            throw new ConflictException("Documents can't be removed after approval. Upload a newer one instead.");
        }

        db.DriverDocuments.Remove(document);
        CarAudit.Record(db, CarAuditEntity.Driver, driverId, "DocumentDeleted", $"{document.DocumentType} document removed", CarActor.Driver(driverId));
        await db.SaveChangesAsync(cancellationToken);
        storage.Delete(document.FileUrl);
    }

    public async Task<DocumentFile> OpenDocumentAsync(int? driverId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await db.DriverDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DriverDocumentId == documentId && (driverId == null || d.DriverId == driverId), cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        var stream = storage.OpenRead(document.FileUrl) ?? throw new NotFoundException("Document file not found.");
        var extension = Path.GetExtension(document.FileUrl);
        return new DocumentFile(stream, document.ContentType, $"{document.DocumentType}-{document.DriverDocumentId}{extension}");
    }

    private async Task<Driver> LoadAsync(int driverId, CancellationToken cancellationToken) =>
        await db.Drivers.Include(d => d.Documents).FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");

    private static void ValidateFile(UploadedImage file, string[] allowedTypes, string typeMessage)
    {
        var errors = new List<string>();
        if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(typeMessage);
        }
        if (file.Length <= 0 || file.Length > MaxFileSizeBytes)
        {
            errors.Add("File must be no larger than 5 MB.");
        }
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }

    internal static DriverProfileDto ToDto(Driver driver) => new(
        driver.DriverId,
        driver.Name,
        driver.PhoneNumber,
        driver.Email,
        driver.Address,
        driver.City,
        driver.BaseLatitude is { } lat && driver.BaseLongitude is { } lng ? new GeoPointDto(lat, lng) : null,
        driver.BaseLocationLabel,
        driver.DrivingLicenceNumber,
        driver.LicenceExpiryDate,
        driver.ExperienceYears,
        driver.ProfilePhotoUrl,
        driver.Status,
        driver.StatusReason,
        driver.SubmittedForReviewAt,
        driver.ApprovedAt,
        driver.CreatedAt,
        CarMapping.MissingForReview(driver),
        driver.Documents.OrderByDescending(d => d.CreatedAt).Select(CarMapping.ToDto).ToList());
}
