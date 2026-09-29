using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars;

/// <summary>Entity → DTO mapping and the "what's still missing" checklists shared by driver and admin screens.</summary>
public static class CarMapping
{
    public static CarPricingDto ToDto(CarPricing pricing) =>
        new CarPricingDto(
            pricing.CarPricingId,
            pricing.PricePerKm,
            pricing.NightHaltPrice,
            pricing.Tiers.OrderBy(t => t.UpToKm ?? int.MaxValue).Select(t => new CarPricingTierDto(t.UpToKm, t.BaseFare)).ToList(),
            pricing.Status,
            pricing.SubmittedByRole,
            pricing.SubmittedAt,
            pricing.ReviewedAt,
            pricing.ReviewNote);

    public static CarPhotoDto ToDto(CarPhoto photo) => new(photo.CarPhotoId, photo.Kind, photo.ImageUrl, photo.DisplayOrder);

    public static DriverDocumentDto ToDto(DriverDocument document) =>
        new(document.DriverDocumentId, document.DocumentType, document.CarId, document.ContentType, document.CreatedAt);

    /// <summary>Car → driver/admin view. Needs Photos, Pricings (with Tiers) and Driver loaded.</summary>
    public static DriverCarDto ToDriverCarDto(Car car)
    {
        var active = car.ActivePricingId is null ? null : car.Pricings.FirstOrDefault(p => p.CarPricingId == car.ActivePricingId);
        var pending = car.Pricings.Where(p => p.Status == CarPricingStatus.Pending).MaxBy(p => p.SubmittedAt);
        var latestRejected = car.Pricings.Where(p => p.Status == CarPricingStatus.Rejected).MaxBy(p => p.SubmittedAt);
        var newerThanRejected = car.Pricings.Any(p =>
            p.Status is CarPricingStatus.Pending or CarPricingStatus.Approved && latestRejected is not null && p.SubmittedAt > latestRejected.SubmittedAt);

        return new DriverCarDto(
            car.CarId,
            car.Brand,
            car.ModelName,
            CarRules.DisplayName(car.Brand, car.ModelName),
            CarRules.Category(car.SeatCapacity),
            car.RegistrationNumber,
            car.FuelType,
            car.SeatCapacity,
            car.HasAc,
            car.Description,
            car.BaseCity,
            car.Status,
            car.StatusReason,
            car.SubmittedForReviewAt,
            car.ApprovedAt,
            IsListed(car),
            car.Photos.OrderBy(p => p.Kind).ThenBy(p => p.DisplayOrder).Select(ToDto).ToList(),
            active is null ? null : ToDto(active),
            pending is null ? null : ToDto(pending),
            latestRejected is null || newerThanRejected ? null : ToDto(latestRejected),
            MissingForReview(car),
            car.CreatedAt);
    }

    /// <summary>Customers can find the car only when the car, its driver and its active pricing are all approved.</summary>
    public static bool IsListed(Car car) =>
        car.Status == CarStatus.Approved && car.Driver.Status == DriverStatus.Approved && car.ActivePricingId is not null;

    public static List<string> MissingForReview(Car car)
    {
        var missing = new List<string>();
        if (car.Photos.Count(p => p.Kind == CarPhotoKind.Exterior) < CarRules.MinExteriorPhotos)
        {
            missing.Add("Add at least one outside (exterior) photo.");
        }
        if (car.Photos.Count(p => p.Kind == CarPhotoKind.Interior) < CarRules.MinInteriorPhotos)
        {
            missing.Add("Add at least one inside (interior) photo.");
        }
        if (!car.Pricings.Any(p => p.Status is CarPricingStatus.Pending or CarPricingStatus.Approved))
        {
            missing.Add("Set your pricing.");
        }
        return missing;
    }

    /// <summary>Needs Documents loaded.</summary>
    public static List<string> MissingForReview(Driver driver)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(driver.Name))
        {
            missing.Add("Add your full name.");
        }
        if (string.IsNullOrWhiteSpace(driver.PhoneNumber))
        {
            missing.Add("Verify your WhatsApp number.");
        }
        if (string.IsNullOrWhiteSpace(driver.Address) || string.IsNullOrWhiteSpace(driver.City))
        {
            missing.Add("Add your address and city.");
        }
        if (string.IsNullOrWhiteSpace(driver.DrivingLicenceNumber))
        {
            missing.Add("Add your driving licence number.");
        }
        if (!driver.Documents.Any(d => d.DocumentType == DriverDocumentType.DrivingLicence))
        {
            missing.Add("Upload a photo of your driving licence.");
        }
        if (string.IsNullOrWhiteSpace(driver.ProfilePhotoUrl))
        {
            missing.Add("Add a profile photo.");
        }
        return missing;
    }
}
