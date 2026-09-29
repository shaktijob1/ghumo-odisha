using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars.Dtos;

public record CarPhotoDto(int CarPhotoId, CarPhotoKind Kind, string ImageUrl, int DisplayOrder);

public record CarPricingTierDto(int? UpToKm, decimal BaseFare);

/// <summary>A pricing version: price per km, night halt and base fare by distance.</summary>
public record CarPricingDto(
    int CarPricingId,
    decimal PricePerKm,
    decimal NightHaltPrice,
    IReadOnlyList<CarPricingTierDto> Tiers,
    CarPricingStatus Status,
    string SubmittedByRole,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNote);

/// <summary>A car as its driver (or an admin) sees it — includes status, pricing versions and review checklist.</summary>
public record DriverCarDto(
    int CarId,
    string Brand,
    string ModelName,
    string DisplayName,
    string Category,
    string RegistrationNumber,
    FuelType FuelType,
    int SeatCapacity,
    bool HasAc,
    string? Description,
    string BaseCity,
    CarStatus Status,
    string? StatusReason,
    DateTime? SubmittedForReviewAt,
    DateTime? ApprovedAt,
    /// <summary>True when customers can find it: car, driver and pricing all approved.</summary>
    bool IsListed,
    IReadOnlyList<CarPhotoDto> Photos,
    CarPricingDto? ActivePricing,
    CarPricingDto? PendingPricing,
    /// <summary>The latest rejected proposal, when it's newer than anything approved or pending.</summary>
    CarPricingDto? RejectedPricing,
    IReadOnlyList<string> MissingForReview,
    DateTime CreatedAt);

public record SaveCarRequest(
    string Brand,
    string ModelName,
    string RegistrationNumber,
    FuelType FuelType,
    int SeatCapacity,
    bool HasAc,
    string? Description,
    string BaseCity);

public record SubmitPricingRequest(
    decimal PricePerKm,
    decimal NightHaltPrice,
    IReadOnlyList<CarPricingTierDto> Tiers);

/// <summary>Public settings the car screens need, so nothing is hardcoded in Angular.</summary>
public record CarRentalSettingsDto(
    decimal BookingAmount,
    decimal DefaultNightHaltPrice,
    IReadOnlyList<int> SeatCapacities,
    int MinDurationHours,
    int MaxDurationHours,
    int MaxEstimatedKm,
    int MinimumLeadMinutes,
    int MaxAdvanceBookingDays);
