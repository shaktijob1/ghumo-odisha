using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Maps;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Maps;

/// <summary>
/// Google Routes API (driving distance) and Geocoding API (PIN code of a point), over plain HttpClient.
/// Results are cached for a while by rounded coordinates: the booking page re-quotes on every change of
/// date / duration, and the same pickup → destination pair shouldn't be paid for each time.
/// </summary>
public class GoogleMapsService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<GoogleMapsOptions> options,
    ILogger<GoogleMapsService> logger) : IMapsService
{
    private const string RoutesUrl = "https://routes.googleapis.com/directions/v2:computeRoutes";
    private const string GeocodeUrl = "https://maps.googleapis.com/maps/api/geocode/json";
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GoogleMapsOptions _options = options.Value;

    public async Task<int> DrivingDistanceMetersAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken = default)
    {
        var key = $"maps:route:{Round(from)}:{Round(to)}";
        if (cache.TryGetValue(key, out int cached))
        {
            return cached;
        }

        var apiKey = RequireKey();
        using var request = new HttpRequestMessage(HttpMethod.Post, RoutesUrl)
        {
            Content = JsonContent.Create(new
            {
                origin = Waypoint(from),
                destination = Waypoint(to),
                travelMode = "DRIVE",
                routingPreference = "TRAFFIC_UNAWARE",
                units = "METRIC"
            }, options: JsonOptions)
        };
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", "routes.distanceMeters");

        using var doc = await SendAsync(request, "Routes", cancellationToken);
        if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
        {
            throw new MapsUnavailableException("We couldn't find a road route between those places. Please pick another location.");
        }

        // Google omits distanceMeters when it is 0 (same point).
        var metres = routes[0].TryGetProperty("distanceMeters", out var d) ? d.GetInt32() : 0;
        cache.Set(key, metres, CacheFor);
        return metres;
    }

    public async Task<string?> PostalCodeAsync(GeoPoint point, CancellationToken cancellationToken = default)
    {
        var key = $"maps:pin:{Round(point)}";
        if (cache.TryGetValue(key, out string? cached))
        {
            return cached;
        }

        var url = $"{GeocodeUrl}?latlng={Coord(point.Latitude)},{Coord(point.Longitude)}&key={Uri.EscapeDataString(RequireKey())}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var doc = await SendAsync(request, "Geocoding", cancellationToken);

        var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status is not ("OK" or "ZERO_RESULTS"))
        {
            logger.LogError("Google Geocoding returned status {Status}", status);
            throw new MapsUnavailableException();
        }

        string? pin = null;
        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            foreach (var result in results.EnumerateArray())
            {
                foreach (var component in result.GetProperty("address_components").EnumerateArray())
                {
                    if (component.GetProperty("types").EnumerateArray().Any(t => t.GetString() == "postal_code"))
                    {
                        pin = component.GetProperty("long_name").GetString();
                        break;
                    }
                }
                if (pin is not null)
                {
                    break;
                }
            }
        }

        cache.Set(key, pin, CacheFor);
        return pin;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, string api, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Google {Api} request failed", api);
            throw new MapsUnavailableException();
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Body carries Google's reason (key not authorised for this API, billing off, …) — log only.
                logger.LogError("Google {Api} returned {StatusCode}: {Body}", api, (int)response.StatusCode, body);
                throw new MapsUnavailableException();
            }
            return JsonDocument.Parse(body);
        }
    }

    private string RequireKey()
    {
        var key = _options.EffectiveServerKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogError("GoogleMaps:ServerApiKey / BrowserApiKey is not configured — cannot compute distances.");
            throw new MapsUnavailableException();
        }
        return key.Trim();
    }

    private static object Waypoint(GeoPoint p) => new { location = new { latLng = new { latitude = p.Latitude, longitude = p.Longitude } } };

    // ~1 m precision: two quotes for the "same" place share a cache entry.
    private static string Round(GeoPoint p) => $"{Coord(p.Latitude)},{Coord(p.Longitude)}";
    private static string Coord(double value) => Math.Round(value, 5).ToString(CultureInfo.InvariantCulture);
}
