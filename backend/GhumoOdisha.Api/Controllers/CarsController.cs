using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Maps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Public Cars endpoints: settings, search, car details and fare quotes. Only approved, listed cars are ever returned.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/cars")]
public class CarsController(ICarCatalogService catalog, IServiceAreaService serviceAreas, IOptions<CarRentalOptions> options) : ControllerBase
{
    /// <summary>Rules the car screens display (booking amount, seat categories, limits) — from config, never hardcoded in Angular.</summary>
    [HttpGet("settings")]
    public ActionResult<ApiResponse<CarRentalSettingsDto>> Settings()
    {
        var o = options.Value;
        return Ok(ApiResponse<CarRentalSettingsDto>.Ok(new CarRentalSettingsDto(
            o.BookingAmount, o.DefaultNightHaltPrice, CarRules.SeatCapacities, o.MinDurationHours, o.MaxDurationHours,
            o.MaxEstimatedKm, o.MinimumLeadMinutes, o.MaxAdvanceBookingDays)));
    }

    /// <summary>?city=Bhubaneswar&amp;date=2026-10-02&amp;time=10:00&amp;durationHours=12&amp;seats=7 (India date/time; all optional).</summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<CarSearchResultsDto>>> Search([FromQuery] CarSearchQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarSearchResultsDto>.Ok(await catalog.SearchAsync(query, cancellationToken)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CarPublicDetailDto>>> Get(int id, [FromQuery] CarWindowQuery window, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarPublicDetailDto>.Ok(await catalog.GetAsync(id, window, cancellationToken)));

    /// <summary>Server-side estimate for the booking page (recalculated whenever date, time, duration or km change).</summary>
    [HttpPost("{id:int}/quote")]
    [EnableRateLimiting("MapsLookup")]
    public async Task<ActionResult<ApiResponse<CarFareQuoteDto>>> Quote(int id, CarQuoteRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarFareQuoteDto>.Ok(await catalog.QuoteAsync(id, request, cancellationToken)));

    /// <summary>Active pickup zones (outlines) for the customer's map. PIN-code-only areas aren't drawn.</summary>
    [HttpGet("service-zones")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicServiceZoneDto>>>> ServiceZones(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IReadOnlyList<PublicServiceZoneDto>>.Ok(await serviceAreas.PublicZonesAsync(cancellationToken)));

    /// <summary>Search with a route: every vehicle for the window with its own fare, measured on the server.</summary>
    [HttpPost("fares")]
    [EnableRateLimiting("MapsLookup")]
    public async Task<ActionResult<ApiResponse<CarFareSearchResultsDto>>> Fares(CarFareSearchRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarFareSearchResultsDto>.Ok(await catalog.SearchWithFaresAsync(request, cancellationToken)));

    /// <summary>Does a car pick up from here? Shown as soon as the customer chooses a pickup — the quote and booking check it again.</summary>
    [HttpPost("pickup-check")]
    [EnableRateLimiting("MapsLookup")]
    public async Task<ActionResult<ApiResponse<PickupCheckDto>>> PickupCheck(PickupCheckRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PickupCheckDto>.Ok(await serviceAreas.CheckAsync(new GeoPoint(request.Latitude, request.Longitude), cancellationToken)));
}
