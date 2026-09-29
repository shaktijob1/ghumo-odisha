using GhumoOdisha.Domain.Entities;

namespace GhumoOdisha.Application.Cars;

/// <summary>One base-fare tier: trips up to <see cref="UpToKm"/> km (null = everything above).</summary>
public record FareTier(int? UpToKm, decimal BaseFare);

/// <summary>The pricing inputs of a fare — always loaded from an approved <see cref="CarPricing"/> row.</summary>
public record FareTerms(decimal PricePerKm, decimal NightHaltPrice, IReadOnlyList<FareTier> Tiers)
{
    public static FareTerms From(CarPricing pricing) => new(
        pricing.PricePerKm,
        pricing.NightHaltPrice,
        pricing.Tiers.Select(t => new FareTier(t.UpToKm, t.BaseFare)).ToList());
}

public record FareBreakdown(
    int Km,
    decimal BaseFare,
    decimal KmCharge,
    int Nights,
    decimal NightHaltCharge,
    decimal AdditionalCharges,
    decimal Total);

/// <summary>
/// The single place a car fare is worked out — the booking estimate and the final fare after the trip
/// both come from here, on the server, from the approved pricing:
///
///   total = base fare (tier for the km) + km × price per km + nights × night halt price + additional charges
///
/// There is no minimum km: short trips are covered by a higher base fare in the lower km tiers. The
/// tiers are configured per car, so "falls until 200 km, then stays flat" is just data: tiers up to
/// 50/100/150/200 km and an open-ended last tier.
/// </summary>
public static class CarFareCalculator
{
    public static FareBreakdown Calculate(FareTerms terms, int km, int nights, decimal additionalCharges = 0m)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(km);
        ArgumentOutOfRangeException.ThrowIfNegative(nights);
        ArgumentOutOfRangeException.ThrowIfNegative(additionalCharges);

        var baseFare = BaseFareFor(terms.Tiers, km);
        var kmCharge = Money(km * terms.PricePerKm);
        var nightHaltCharge = Money(nights * terms.NightHaltPrice);
        var additional = Money(additionalCharges);

        return new FareBreakdown(km, baseFare, kmCharge, nights, nightHaltCharge, additional,
            baseFare + kmCharge + nightHaltCharge + additional);
    }

    /// <summary>Base fare of the first tier that covers <paramref name="km"/>; the open-ended tier covers the rest.</summary>
    public static decimal BaseFareFor(IReadOnlyList<FareTier> tiers, int km)
    {
        if (tiers.Count == 0)
        {
            throw new InvalidOperationException("Pricing has no base fare tiers.");
        }

        var ordered = tiers.OrderBy(t => t.UpToKm ?? int.MaxValue).ToList();
        var tier = ordered.FirstOrDefault(t => t.UpToKm is null || km <= t.UpToKm) ?? ordered[^1];
        return Money(tier.BaseFare);
    }

    /// <summary>
    /// Problems with a proposed tier list, empty when valid: at least one tier, exactly one open-ended
    /// ("and above") tier, distance limits positive and all different.
    /// </summary>
    public static List<string> ValidateTiers(IReadOnlyCollection<FareTier> tiers)
    {
        var errors = new List<string>();
        if (tiers.Count == 0)
        {
            errors.Add("Add at least one base fare.");
            return errors;
        }
        if (tiers.Count > 10)
        {
            errors.Add("Use at most 10 base fare ranges.");
        }

        var openEnded = tiers.Count(t => t.UpToKm is null);
        if (openEnded != 1)
        {
            errors.Add("Exactly one base fare range must be open-ended (\"and above\").");
        }

        var limits = tiers.Where(t => t.UpToKm is not null).Select(t => t.UpToKm!.Value).ToList();
        if (limits.Any(l => l <= 0))
        {
            errors.Add("Distance limits must be more than 0 km.");
        }
        if (limits.Distinct().Count() != limits.Count)
        {
            errors.Add("Two base fare ranges end at the same distance.");
        }
        if (tiers.Any(t => t.BaseFare < 0))
        {
            errors.Add("Base fare can't be negative.");
        }

        return errors;
    }

    private static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
