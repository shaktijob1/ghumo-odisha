using System.Globalization;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Cars;

/// <summary>
/// Creating a new pricing version — the only way pricing ever changes. The new row starts Pending;
/// any earlier pending proposal is withdrawn (kept for history, never deleted), and the currently
/// approved pricing stays active until an admin approves the new one.
/// </summary>
public static class CarPricingProposals
{
    /// <summary>Adds a Pending pricing row to <paramref name="car"/> (needs Pricings loaded). The caller saves.</summary>
    public static CarPricing Propose(IGhumoOdishaDbContext db, Car car, SubmitPricingRequest request, CarActor actor)
    {
        var tiers = request.Tiers.Select(t => new FareTier(t.UpToKm, t.BaseFare)).ToList();
        var errors = CarFareCalculator.ValidateTiers(tiers);
        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var now = DateTime.UtcNow;
        foreach (var pending in car.Pricings.Where(p => p.Status == CarPricingStatus.Pending))
        {
            pending.Status = CarPricingStatus.Withdrawn;
        }

        var pricing = new CarPricing
        {
            CarId = car.CarId,
            PricePerKm = request.PricePerKm,
            NightHaltPrice = request.NightHaltPrice,
            Status = CarPricingStatus.Pending,
            SubmittedByRole = actor.Role,
            SubmittedById = actor.Id ?? 0,
            SubmittedAt = now,
            Tiers = tiers.Select(t => new CarPricingTier { UpToKm = t.UpToKm, BaseFare = t.BaseFare }).ToList()
        };
        car.Pricings.Add(pricing);

        var active = car.Pricings.FirstOrDefault(p => p.CarPricingId == car.ActivePricingId && car.ActivePricingId is not null);
        CarAudit.Record(db, CarAuditEntity.Pricing, car.CarId, "PricingSubmitted", "New pricing submitted for approval", actor,
            active is null ? null : Summarize(active), Summarize(pricing));
        return pricing;
    }

    /// <summary>"₹13/km · Night halt ₹400 · Base: up to 50 km ₹800, up to 100 km ₹600, above ₹300".</summary>
    public static string Summarize(CarPricing pricing)
    {
        var inr = CultureInfo.GetCultureInfo("en-IN");
        var tiers = string.Join(", ", pricing.Tiers.OrderBy(t => t.UpToKm ?? int.MaxValue).Select(t =>
            (t.UpToKm is null ? "above" : $"up to {t.UpToKm} km") + " ₹" + t.BaseFare.ToString("0.##", inr)));
        return $"₹{pricing.PricePerKm.ToString("0.##", inr)}/km · Night halt ₹{pricing.NightHaltPrice.ToString("0.##", inr)} · Base: {tiers}";
    }
}
