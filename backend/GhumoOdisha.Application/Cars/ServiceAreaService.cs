using System.Text.Json;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Maps;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Cars;

public interface IServiceAreaService
{
    Task<IReadOnlyList<ServiceAreaDto>> ListAsync(CancellationToken cancellationToken = default);
    /// <summary>Active drawn zones only (names + outlines) — shaded on the customer's map picker.</summary>
    Task<IReadOnlyList<PublicServiceZoneDto>> PublicZonesAsync(CancellationToken cancellationToken = default);
    Task<ServiceAreaDto> CreateAsync(SaveServiceAreaRequest request, CancellationToken cancellationToken = default);
    Task<ServiceAreaDto> UpdateAsync(int serviceAreaId, SaveServiceAreaRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int serviceAreaId, CancellationToken cancellationToken = default);

    /// <summary>Is a pickup at this point inside an active area (drawn zone, or PIN code list)?</summary>
    Task<PickupCheckDto> CheckAsync(GeoPoint point, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin-defined pickup areas for cars. The check always runs on the server — the booking page only
/// shows the answer — and the PIN code of a point comes from Google, not from the browser.
/// </summary>
public class ServiceAreaService(IGhumoOdishaDbContext db, IMapsService maps) : IServiceAreaService
{
    public const string OutsideAreaMessage = "Sorry, cars aren't available for pickup from this area yet.";

    public async Task<IReadOnlyList<ServiceAreaDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var areas = await db.ServiceAreas.AsNoTracking().OrderByDescending(a => a.IsActive).ThenBy(a => a.Name).ToListAsync(cancellationToken);
        return areas.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PublicServiceZoneDto>> PublicZonesAsync(CancellationToken cancellationToken = default)
    {
        var areas = await db.ServiceAreas.AsNoTracking().Where(a => a.IsActive && a.BoundaryJson != null).OrderBy(a => a.Name).ToListAsync(cancellationToken);
        return areas
            .Select(a => new PublicServiceZoneDto(a.Name, ParseBoundary(a.BoundaryJson).Select(p => new GeoPointDto(p.Latitude, p.Longitude)).ToList()))
            .ToList();
    }

    public async Task<ServiceAreaDto> CreateAsync(SaveServiceAreaRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var area = new ServiceArea { CreatedAt = now };
        Apply(area, request, now);
        db.ServiceAreas.Add(area);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(area);
    }

    public async Task<ServiceAreaDto> UpdateAsync(int serviceAreaId, SaveServiceAreaRequest request, CancellationToken cancellationToken = default)
    {
        var area = await db.ServiceAreas.FirstOrDefaultAsync(a => a.ServiceAreaId == serviceAreaId, cancellationToken)
            ?? throw new NotFoundException("Service area not found.");
        Apply(area, request, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(area);
    }

    public async Task DeleteAsync(int serviceAreaId, CancellationToken cancellationToken = default)
    {
        var deleted = await db.ServiceAreas.Where(a => a.ServiceAreaId == serviceAreaId).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            throw new NotFoundException("Service area not found.");
        }
    }

    public async Task<PickupCheckDto> CheckAsync(GeoPoint point, CancellationToken cancellationToken = default)
    {
        if (!point.IsValid)
        {
            throw new ValidationAppException(["Choose a valid pickup location."]);
        }

        var areas = await db.ServiceAreas.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        if (areas.Count == 0)
        {
            return new PickupCheckDto(true, null, null);
        }

        // Drawn zones first: free, no Google call.
        foreach (var area in areas)
        {
            if (GeoMath.IsInside(point, ParseBoundary(area.BoundaryJson)))
            {
                return new PickupCheckDto(true, area.Name, null);
            }
        }

        var withPincodes = areas.Where(a => !string.IsNullOrEmpty(a.Pincodes)).ToList();
        if (withPincodes.Count > 0)
        {
            var pin = await maps.PostalCodeAsync(point, cancellationToken);
            if (pin is not null)
            {
                var match = withPincodes.FirstOrDefault(a => a.Pincodes!.Split(',').Contains(pin));
                if (match is not null)
                {
                    return new PickupCheckDto(true, match.Name, null);
                }
            }
        }

        return new PickupCheckDto(false, null, OutsideAreaMessage);
    }

    private static void Apply(ServiceArea area, SaveServiceAreaRequest request, DateTime now)
    {
        var boundary = request.Boundary ?? [];
        var pincodes = NormalizePincodes(request.Pincodes);
        if (boundary.Count == 0 && pincodes.Count == 0)
        {
            throw new ValidationAppException(["Draw a zone on the map or add at least one PIN code."]);
        }

        area.Name = request.Name.Trim();
        area.IsActive = request.IsActive;
        area.BoundaryJson = boundary.Count == 0
            ? null
            : JsonSerializer.Serialize(boundary.Select(p => new[] { Math.Round(p.Latitude, 6), Math.Round(p.Longitude, 6) }));
        area.Pincodes = pincodes.Count == 0 ? null : string.Join(',', pincodes);
        area.UpdatedAt = now;
    }

    internal static List<string> NormalizePincodes(IEnumerable<string>? pincodes) =>
        (pincodes ?? []).Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().Order().ToList();

    internal static List<GeoPoint> ParseBoundary(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }
        var pairs = JsonSerializer.Deserialize<double[][]>(json) ?? [];
        return pairs.Where(p => p.Length == 2).Select(p => new GeoPoint(p[0], p[1])).ToList();
    }

    private static ServiceAreaDto ToDto(ServiceArea a) => new(
        a.ServiceAreaId,
        a.Name,
        a.IsActive,
        ParseBoundary(a.BoundaryJson).Select(p => new GeoPointDto(p.Latitude, p.Longitude)).ToList(),
        string.IsNullOrEmpty(a.Pincodes) ? [] : a.Pincodes.Split(','),
        a.CreatedAt,
        a.UpdatedAt);
}
