using System.Globalization;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Cars;

public interface IDriverCarService
{
    Task<IReadOnlyList<DriverCarDto>> ListAsync(int driverId, CancellationToken cancellationToken = default);
    Task<DriverCarDto> GetAsync(int driverId, int carId, CancellationToken cancellationToken = default);
    Task<DriverCarDto> CreateAsync(int driverId, SaveCarRequest request, CancellationToken cancellationToken = default);
    Task<DriverCarDto> UpdateAsync(int driverId, int carId, SaveCarRequest request, CancellationToken cancellationToken = default);
    Task<DriverCarDto> AddPhotoAsync(int driverId, int carId, CarPhotoKind kind, UploadedImage image, CancellationToken cancellationToken = default);
    Task<DriverCarDto> DeletePhotoAsync(int driverId, int carId, int photoId, CancellationToken cancellationToken = default);
    Task<DriverCarDto> SubmitForReviewAsync(int driverId, int carId, CancellationToken cancellationToken = default);
    Task<DriverCarDto> DeactivateAsync(int driverId, int carId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarPricingDto>> GetPricingHistoryAsync(int driverId, int carId, CancellationToken cancellationToken = default);
    Task<DriverCarDto> SubmitPricingAsync(int driverId, int carId, SubmitPricingRequest request, CancellationToken cancellationToken = default);

    // Admin — same rules as the driver's own calls, recorded with the admin as the actor.
    /// <summary>Registers a car for a driver. It starts as waiting for review, so the admin can publish it (approve) once photos and pricing are in.</summary>
    Task<DriverCarDto> AdminCreateAsync(int adminId, int driverId, SaveCarRequest request, CancellationToken cancellationToken = default);
    Task<DriverCarDto> AdminAddPhotoAsync(int adminId, int carId, CarPhotoKind kind, UploadedImage image, CancellationToken cancellationToken = default);
    Task<DriverCarDto> AdminDeletePhotoAsync(int adminId, int carId, int photoId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A driver's own cars: registration, photos, review submission and pricing proposals. Every query is
/// scoped to the driver id from the JWT, so another driver's car simply "doesn't exist" (404).
/// A driver can propose and resubmit, but never approve — status only moves to Approved in the admin service.
/// </summary>
public class DriverCarService(IGhumoOdishaDbContext db, IImageStorage storage) : IDriverCarService
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private static readonly string[] ImageContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<IReadOnlyList<DriverCarDto>> ListAsync(int driverId, CancellationToken cancellationToken = default)
    {
        var cars = await CarsWithDetails().Where(c => c.DriverId == driverId).OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
        return cars.Select(CarMapping.ToDriverCarDto).ToList();
    }

    public async Task<DriverCarDto> GetAsync(int driverId, int carId, CancellationToken cancellationToken = default) =>
        CarMapping.ToDriverCarDto(await LoadOwnCarAsync(driverId, carId, cancellationToken));

    public async Task<DriverCarDto> CreateAsync(int driverId, SaveCarRequest request, CancellationToken cancellationToken = default)
    {
        var car = await CreateCarAsync(driverId, request, CarActor.Driver(driverId), cancellationToken);
        return await GetAsync(driverId, car.CarId, cancellationToken);
    }

    public async Task<DriverCarDto> AdminCreateAsync(int adminId, int driverId, SaveCarRequest request, CancellationToken cancellationToken = default)
    {
        var car = await CreateCarAsync(driverId, request, CarActor.Admin(adminId), cancellationToken);
        return CarMapping.ToDriverCarDto(await LoadCarAsync(null, car.CarId, cancellationToken));
    }

    private async Task<Car> CreateCarAsync(int driverId, SaveCarRequest request, CarActor actor, CancellationToken cancellationToken)
    {
        var byAdmin = actor.Role == "Admin";
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId, cancellationToken)
            ?? throw new NotFoundException("Driver not found.");
        if (driver.Status == DriverStatus.Suspended)
        {
            throw new ConflictException(byAdmin
                ? "This driver is suspended. Lift the suspension before adding a car."
                : "Your account is suspended. Please contact Ghumo Odisha.");
        }

        var registration = await NormalizeFreeRegistrationAsync(request.RegistrationNumber, null, cancellationToken);
        var now = DateTime.UtcNow;
        var car = new Car
        {
            DriverId = driverId,
            Status = CarStatus.Pending,
            // An admin-added car needs no driver submission: it's ready for the admin to publish.
            SubmittedForReviewAt = byAdmin ? now : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        Apply(car, request, registration);
        db.Cars.Add(car);
        await SaveUniqueAsync(cancellationToken);

        CarAudit.Record(db, CarAuditEntity.Car, car.CarId, "CarRegistered", byAdmin ? "Car added by admin" : "Car registered", actor, newValue: Describe(car));
        await db.SaveChangesAsync(cancellationToken);
        return car;
    }

    public async Task<DriverCarDto> UpdateAsync(int driverId, int carId, SaveCarRequest request, CancellationToken cancellationToken = default)
    {
        var car = await LoadOwnCarAsync(driverId, carId, cancellationToken);
        EnsureNotSuspended(car);

        var registration = await NormalizeFreeRegistrationAsync(request.RegistrationNumber, carId, cancellationToken);
        var before = Describe(car);
        var detailsChanged = car.Brand != request.Brand.Trim() || car.ModelName != request.ModelName.Trim() || car.RegistrationNumber != registration
            || car.FuelType != request.FuelType || car.SeatCapacity != request.SeatCapacity || car.HasAc != request.HasAc
            || car.BaseCity != CarRules.NormalizeCity(request.BaseCity);

        Apply(car, request, registration);
        car.UpdatedAt = DateTime.UtcNow;

        // An approved car whose facts change is checked again (and leaves search until then);
        // a description-only edit goes live straight away.
        if (detailsChanged && car.Status == CarStatus.Approved)
        {
            car.Status = CarStatus.Pending;
            car.SubmittedForReviewAt = DateTime.UtcNow;
            CarAudit.Record(db, CarAuditEntity.Car, carId, "CarResubmitted", "Sent for review again after details changed",
                CarActor.Driver(driverId), nameof(CarStatus.Approved), nameof(CarStatus.Pending));
        }
        if (detailsChanged || before != Describe(car))
        {
            CarAudit.Record(db, CarAuditEntity.Car, carId, "CarUpdated", "Car details changed", CarActor.Driver(driverId), before, Describe(car));
        }

        await SaveUniqueAsync(cancellationToken);
        return CarMapping.ToDriverCarDto(car);
    }

    public Task<DriverCarDto> AddPhotoAsync(int driverId, int carId, CarPhotoKind kind, UploadedImage image, CancellationToken cancellationToken = default) =>
        AddPhotoCoreAsync(driverId, carId, kind, image, CarActor.Driver(driverId), cancellationToken);

    public Task<DriverCarDto> AdminAddPhotoAsync(int adminId, int carId, CarPhotoKind kind, UploadedImage image, CancellationToken cancellationToken = default) =>
        AddPhotoCoreAsync(null, carId, kind, image, CarActor.Admin(adminId), cancellationToken);

    /// <summary><paramref name="driverId"/> null = admin (any car, suspended cars included).</summary>
    private async Task<DriverCarDto> AddPhotoCoreAsync(int? driverId, int carId, CarPhotoKind kind, UploadedImage image, CarActor actor, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ValidationAppException(["Choose whether this is an outside or inside photo."]);
        }
        ValidateImage(image);

        var car = await LoadCarAsync(driverId, carId, cancellationToken);
        if (driverId is not null) EnsureNotSuspended(car);
        var sameKind = car.Photos.Where(p => p.Kind == kind).ToList();
        if (sameKind.Count >= CarRules.MaxPhotosPerKind)
        {
            throw new ConflictException($"You can add up to {CarRules.MaxPhotosPerKind} {(kind == CarPhotoKind.Exterior ? "outside" : "inside")} photos.");
        }

        var url = await storage.SaveAsync(image.Content, image.FileName, image.ContentType, "cars", cancellationToken);
        car.Photos.Add(new CarPhoto
        {
            CarId = carId,
            Kind = kind,
            ImageUrl = url,
            DisplayOrder = sameKind.Count == 0 ? 0 : sameKind.Max(p => p.DisplayOrder) + 1,
            CreatedAt = DateTime.UtcNow
        });
        CarAudit.Record(db, CarAuditEntity.Car, carId, "CarPhotoAdded", $"{kind} photo added", actor, newValue: url);
        await db.SaveChangesAsync(cancellationToken);
        return CarMapping.ToDriverCarDto(car);
    }

    public Task<DriverCarDto> DeletePhotoAsync(int driverId, int carId, int photoId, CancellationToken cancellationToken = default) =>
        DeletePhotoCoreAsync(driverId, carId, photoId, CarActor.Driver(driverId), cancellationToken);

    public Task<DriverCarDto> AdminDeletePhotoAsync(int adminId, int carId, int photoId, CancellationToken cancellationToken = default) =>
        DeletePhotoCoreAsync(null, carId, photoId, CarActor.Admin(adminId), cancellationToken);

    private async Task<DriverCarDto> DeletePhotoCoreAsync(int? driverId, int carId, int photoId, CarActor actor, CancellationToken cancellationToken)
    {
        var car = await LoadCarAsync(driverId, carId, cancellationToken);
        if (driverId is not null) EnsureNotSuspended(car);
        var photo = car.Photos.FirstOrDefault(p => p.CarPhotoId == photoId) ?? throw new NotFoundException("Photo not found.");

        // Once submitted or approved, the car must keep at least one photo of each kind.
        var inReviewOrLive = car.Status == CarStatus.Approved || car.SubmittedForReviewAt is not null && car.Status == CarStatus.Pending;
        var minimum = photo.Kind == CarPhotoKind.Exterior ? CarRules.MinExteriorPhotos : CarRules.MinInteriorPhotos;
        if (inReviewOrLive && car.Photos.Count(p => p.Kind == photo.Kind) <= minimum)
        {
            throw new ConflictException("Add another photo of this kind before removing this one.");
        }

        car.Photos.Remove(photo);
        db.CarPhotos.Remove(photo);
        CarAudit.Record(db, CarAuditEntity.Car, carId, "CarPhotoDeleted", $"{photo.Kind} photo removed", actor, oldValue: photo.ImageUrl);
        await db.SaveChangesAsync(cancellationToken);
        storage.Delete(photo.ImageUrl);
        return CarMapping.ToDriverCarDto(car);
    }

    public async Task<DriverCarDto> SubmitForReviewAsync(int driverId, int carId, CancellationToken cancellationToken = default)
    {
        var car = await LoadOwnCarAsync(driverId, carId, cancellationToken);
        EnsureNotSuspended(car);

        if (car.Driver.Status == DriverStatus.Suspended)
        {
            throw new ConflictException("Your account is suspended. Please contact Ghumo Odisha.");
        }
        if (car.Driver.Status == DriverStatus.Rejected)
        {
            throw new ConflictException("Please update your profile and send it for review first.");
        }
        if (car.Status == CarStatus.Approved)
        {
            throw new ConflictException("This car is already approved.");
        }
        if (car.Status == CarStatus.Pending && car.SubmittedForReviewAt is not null)
        {
            throw new ConflictException("This car is already waiting for review.");
        }

        var missing = CarMapping.MissingForReview(car);
        if (missing.Count > 0)
        {
            throw new ValidationAppException(missing);
        }

        var old = car.Status;
        car.Status = CarStatus.Pending;
        car.StatusReason = null;
        car.SubmittedForReviewAt = DateTime.UtcNow;
        car.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Car, carId, "CarSubmitted", "Car sent for review", CarActor.Driver(driverId), old.ToString(), nameof(CarStatus.Pending));
        await db.SaveChangesAsync(cancellationToken);
        return CarMapping.ToDriverCarDto(car);
    }

    public async Task<DriverCarDto> DeactivateAsync(int driverId, int carId, CancellationToken cancellationToken = default)
    {
        var car = await LoadOwnCarAsync(driverId, carId, cancellationToken);
        EnsureNotSuspended(car);
        if (car.Status == CarStatus.Inactive)
        {
            return CarMapping.ToDriverCarDto(car);
        }

        if (await CarAvailability.HasUpcomingBookingsAsync(db, carId, DateTime.UtcNow, cancellationToken))
        {
            throw new ConflictException("This car has upcoming bookings. Please contact Ghumo Odisha to move or cancel them first.");
        }

        var old = car.Status;
        car.Status = CarStatus.Inactive;
        car.StatusReason = "Taken off by the driver.";
        car.SubmittedForReviewAt = null;
        car.UpdatedAt = DateTime.UtcNow;
        CarAudit.Record(db, CarAuditEntity.Car, carId, "CarDeactivated", "Car taken off the site", CarActor.Driver(driverId), old.ToString(), nameof(CarStatus.Inactive));
        await db.SaveChangesAsync(cancellationToken);
        return CarMapping.ToDriverCarDto(car);
    }

    public async Task<IReadOnlyList<CarPricingDto>> GetPricingHistoryAsync(int driverId, int carId, CancellationToken cancellationToken = default)
    {
        var car = await LoadOwnCarAsync(driverId, carId, cancellationToken);
        return car.Pricings.OrderByDescending(p => p.SubmittedAt).ThenByDescending(p => p.CarPricingId).Select(CarMapping.ToDto).ToList();
    }

    public async Task<DriverCarDto> SubmitPricingAsync(int driverId, int carId, SubmitPricingRequest request, CancellationToken cancellationToken = default)
    {
        var car = await LoadOwnCarAsync(driverId, carId, cancellationToken);
        EnsureNotSuspended(car);

        CarPricingProposals.Propose(db, car, request, CarActor.Driver(driverId));
        await db.SaveChangesAsync(cancellationToken);
        return CarMapping.ToDriverCarDto(car);
    }

    // ---------- helpers ----------

    private IQueryable<Car> CarsWithDetails() =>
        db.Cars
            .Include(c => c.Driver)
            .Include(c => c.Photos)
            .Include(c => c.Pricings).ThenInclude(p => p.Tiers)
            .AsSplitQuery();

    private Task<Car> LoadOwnCarAsync(int driverId, int carId, CancellationToken cancellationToken) =>
        LoadCarAsync(driverId, carId, cancellationToken);

    /// <summary>The driver's own car, or (driverId null = admin) any car.</summary>
    private async Task<Car> LoadCarAsync(int? driverId, int carId, CancellationToken cancellationToken) =>
        await CarsWithDetails().FirstOrDefaultAsync(c => c.CarId == carId && (driverId == null || c.DriverId == driverId), cancellationToken)
            ?? throw new NotFoundException("Car not found.");

    private static void EnsureNotSuspended(Car car)
    {
        if (car.Status == CarStatus.Suspended)
        {
            throw new ConflictException("This car is suspended. Please contact Ghumo Odisha.");
        }
    }

    private async Task<string> NormalizeFreeRegistrationAsync(string raw, int? carId, CancellationToken cancellationToken)
    {
        var registration = CarRules.NormalizeRegistration(raw);
        if (!CarRules.IsValidRegistration(registration))
        {
            throw new ValidationAppException(["Enter a valid registration number, e.g. OD02AB1234."]);
        }
        if (await db.Cars.AnyAsync(c => c.RegistrationNumber == registration && c.CarId != carId, cancellationToken))
        {
            throw new ConflictException("A car with this registration number is already registered.");
        }
        return registration;
    }

    /// <summary>Saves, turning a registration-number unique-index race into a friendly conflict.</summary>
    private async Task SaveUniqueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
        {
            throw new ConflictException("A car with this registration number is already registered.");
        }
    }

    private static void Apply(Car car, SaveCarRequest request, string registration)
    {
        car.Brand = request.Brand.Trim();
        car.ModelName = request.ModelName.Trim();
        car.RegistrationNumber = registration;
        car.FuelType = request.FuelType;
        car.SeatCapacity = request.SeatCapacity;
        car.HasAc = request.HasAc;
        car.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        car.BaseCity = CarRules.NormalizeCity(request.BaseCity);
    }

    private static string Describe(Car car) => string.Join(" · ",
        CarRules.DisplayName(car.Brand, car.ModelName), car.RegistrationNumber, car.FuelType, $"{car.SeatCapacity} seats",
        car.HasAc ? "AC" : "Non-AC", car.BaseCity, (car.Description ?? "").Length.ToString(CultureInfo.InvariantCulture) + " chars description");

    private static void ValidateImage(UploadedImage image)
    {
        var errors = new List<string>();
        if (!ImageContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Only JPG, PNG or WebP photos are allowed.");
        }
        if (image.Length <= 0 || image.Length > MaxImageSizeBytes)
        {
            errors.Add("Photo must be no larger than 5 MB.");
        }
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }
}
