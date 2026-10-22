using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

/// <param name="PricePerPerson">This departure's own price; null = the trip's price.</param>
public record AddDateSlotRequest(DateOnly StartDate, DateOnly EndDate, int TotalSeats, decimal? PricePerPerson = null);

/// <param name="PricePerPerson">This departure's own price; null = the trip's price.</param>
public record UpdateDateSlotRequest(DateOnly StartDate, DateOnly EndDate, int TotalSeats, TripDateSlotStatus Status, decimal? PricePerPerson = null);

/// <summary>Admin: one price for every departure of the trip that starts in <paramref name="Month"/> ("2026-11").</summary>
/// <param name="PricePerPerson">The new price; null puts those departures back on the trip's price.</param>
public record SetMonthPriceRequest(string Month, decimal? PricePerPerson);
