using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Cars;

public interface ICarCatalogService
{
    Task<CarSearchResultsDto> SearchAsync(CarSearchQuery query, CancellationToken cancellationToken = default);
    Task<CarPublicDetailDto> GetAsync(int carId, CarWindowQuery window, CancellationToken cancellationToken = default);
    Task<CarFareQuoteDto> QuoteAsync(int carId, CarQuoteRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// What customers can see: only listed cars (car, driver and active pricing approved) — an unapproved
/// car is a 404 here, never a hidden flag. Prices are always calculated from the approved pricing.
/// </summary>
public class CarCatalogService(IGhumoOdishaDbContext db, IOptions<CarRentalOptions> options) : ICarCatalogService
{
    private readonly CarRentalOptions _options = options.Value;

    public async Task<CarSearchResultsDto> SearchAsync(CarSearchQuery query, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var window = query is { Date: not null, Time: not null, DurationHours: not null }
            ? CarRentalWindow.Resolve(query.Date.Value, query.Time.Value, query.DurationHours.Value, _options, now)
            : null;

        var cars = ListedCars();
        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = CarRules.NormalizeCity(query.City);
            cars = cars.Where(c => c.BaseCity == city);
        }
        if (query.Seats is not null)
        {
            cars = cars.Where(c => c.SeatCapacity == query.Seats);
        }

        var list = await cars.ToListAsync(cancellationToken);
        var busy = window is null
            ? []
            : await BusyCarIdsAsync(list.Select(c => c.CarId).ToList(), window, now, cancellationToken);

        var results = list
            .Select(c => ToResult(c, !busy.Contains(c.CarId)))
            .OrderByDescending(r => r.IsAvailable)
            .ThenBy(r => r.SeatCapacity)
            .ThenBy(r => r.PricePerKm)
            .ToList();

        var locations = await db.Cars.AsNoTracking().Listed().Select(c => c.BaseCity).Distinct().OrderBy(c => c).ToListAsync(cancellationToken);
        return new CarSearchResultsDto(results, locations);
    }

    public async Task<CarPublicDetailDto> GetAsync(int carId, CarWindowQuery query, CancellationToken cancellationToken = default)
    {
        var car = await ListedCars(allPhotos: true).FirstOrDefaultAsync(c => c.CarId == carId, cancellationToken)
            ?? throw new NotFoundException("This car isn't available for booking.");

        var now = DateTime.UtcNow;
        var window = query is { Date: not null, Time: not null, DurationHours: not null }
            ? CarRentalWindow.Resolve(query.Date.Value, query.Time.Value, query.DurationHours.Value, _options, now)
            : null;
        var available = window is null || (await BusyCarIdsAsync([carId], window, now, cancellationToken)).Count == 0;

        var pricing = car.ActivePricing!;
        return new CarPublicDetailDto(
            ToResult(car, available),
            car.Description,
            car.Photos.OrderBy(p => p.Kind).ThenBy(p => p.DisplayOrder).Select(CarMapping.ToDto).ToList(),
            pricing.Tiers.OrderBy(t => t.UpToKm ?? int.MaxValue).Select(t => new CarPricingTierDto(t.UpToKm, t.BaseFare)).ToList(),
            new PublicDriverDto(FirstName(car.Driver.Name), car.Driver.ProfilePhotoUrl, car.Driver.ExperienceYears));
    }

    public async Task<CarFareQuoteDto> QuoteAsync(int carId, CarQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var car = await ListedCars().FirstOrDefaultAsync(c => c.CarId == carId, cancellationToken)
            ?? throw new NotFoundException("This car isn't available for booking.");

        var now = DateTime.UtcNow;
        var window = CarRentalWindow.Resolve(request.PickupDate, request.PickupTime, request.DurationHours, _options, now);
        ValidateKm(request.EstimatedKm, _options);

        var busy = await BusyCarIdsAsync([carId], window, now, cancellationToken);
        return BuildQuote(car.CarId, car.ActivePricing!, window, request.EstimatedKm, _options.BookingAmount,
            busy.Count == 0 ? null : "This car is already booked for part of that time. Try another time or car.");
    }

    internal static CarFareQuoteDto BuildQuote(int carId, CarPricing pricing, CarRentalWindow window, int estimatedKm, decimal bookingAmount, string? unavailableReason)
    {
        var fare = CarFareCalculator.Calculate(FareTerms.From(pricing), estimatedKm, window.Nights);
        return new CarFareQuoteDto(carId, window.StartUtc, window.EndUtc, window.DurationHours, estimatedKm,
            pricing.PricePerKm, fare.BaseFare, fare.KmCharge, fare.Nights, pricing.NightHaltPrice, fare.NightHaltCharge,
            fare.Total, bookingAmount, Math.Max(fare.Total - bookingAmount, 0m), unavailableReason is null, unavailableReason);
    }

    internal static void ValidateKm(int km, CarRentalOptions options)
    {
        if (km < 1 || km > options.MaxEstimatedKm)
        {
            throw new ValidationAppException([$"Enter an approximate distance between 1 and {options.MaxEstimatedKm:N0} km."]);
        }
    }

    private IQueryable<Car> ListedCars(bool allPhotos = false) =>
        db.Cars.AsNoTracking()
            .Listed()
            .Include(c => c.Driver)
            .Include(c => c.ActivePricing!).ThenInclude(p => p.Tiers)
            .Include(c => c.Photos.Where(p => allPhotos || p.Kind == CarPhotoKind.Exterior))
            .AsSplitQuery();

    private async Task<HashSet<int>> BusyCarIdsAsync(List<int> carIds, CarRentalWindow window, DateTime now, CancellationToken cancellationToken)
    {
        if (carIds.Count == 0)
        {
            return [];
        }
        var ids = await db.CarBookings.AsNoTracking()
            .Where(b => carIds.Contains(b.CarId))
            .Overlapping(window.StartUtc, window.EndUtc, _options.TurnaroundBufferMinutes, now)
            .Select(b => b.CarId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. ids];
    }

    private static CarSearchResultDto ToResult(Car car, bool available)
    {
        var pricing = car.ActivePricing!;
        return new CarSearchResultDto(
            car.CarId,
            CarRules.DisplayName(car.Brand, car.ModelName),
            CarRules.Category(car.SeatCapacity),
            car.Brand,
            car.ModelName,
            car.FuelType,
            car.SeatCapacity,
            car.HasAc,
            car.BaseCity,
            car.Photos.Where(p => p.Kind == CarPhotoKind.Exterior).OrderBy(p => p.DisplayOrder).Select(p => p.ImageUrl).FirstOrDefault()
                ?? car.Photos.OrderBy(p => p.DisplayOrder).Select(p => p.ImageUrl).FirstOrDefault(),
            pricing.PricePerKm,
            pricing.Tiers.Min(t => t.BaseFare),
            pricing.Tiers.Max(t => t.BaseFare),
            pricing.NightHaltPrice,
            available);
    }

    private static string FirstName(string name)
    {
        var trimmed = name.Trim();
        var space = trimmed.IndexOf(' ');
        return space > 0 ? trimmed[..space] : trimmed;
    }
}
