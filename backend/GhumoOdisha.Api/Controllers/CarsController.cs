using GhumoOdisha.Application.Cars;
using GhumoOdisha.Application.Cars.Dtos;
using GhumoOdisha.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

/// <summary>Public Cars endpoints: settings, search, car details and fare quotes. Only approved, listed cars are ever returned.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/cars")]
public class CarsController(ICarCatalogService catalog, IOptions<CarRentalOptions> options) : ControllerBase
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
    public async Task<ActionResult<ApiResponse<CarFareQuoteDto>>> Quote(int id, CarQuoteRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<CarFareQuoteDto>.Ok(await catalog.QuoteAsync(id, request, cancellationToken)));
}
