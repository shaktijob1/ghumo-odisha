using GhumoOdisha.Application.Maps;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>
/// Deterministic stand-in for Google: every 0.01° of latitude or longitude between two points is exactly
/// 1 km by "road", so tests can place points at known distances. PIN codes are looked up in a dictionary.
/// </summary>
public class FakeMapsService : IMapsService
{
    public Dictionary<(double, double), string> PostalCodes { get; } = [];
    public int DistanceCalls { get; private set; }
    public int PostalCodeCalls { get; private set; }

    public Task<int> DrivingDistanceMetersAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken = default)
    {
        DistanceCalls++;
        var degrees = Math.Abs(from.Latitude - to.Latitude) + Math.Abs(from.Longitude - to.Longitude);
        return Task.FromResult((int)Math.Round(degrees * 100_000));
    }

    public Task<string?> PostalCodeAsync(GeoPoint point, CancellationToken cancellationToken = default)
    {
        PostalCodeCalls++;
        return Task.FromResult(PostalCodes.TryGetValue((point.Latitude, point.Longitude), out var pin) ? pin : null);
    }
}
