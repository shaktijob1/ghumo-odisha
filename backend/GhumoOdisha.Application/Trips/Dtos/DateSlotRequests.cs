using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

public record AddDateSlotRequest(DateOnly StartDate, DateOnly EndDate, int TotalSeats);

public record UpdateDateSlotRequest(DateOnly StartDate, DateOnly EndDate, int TotalSeats, TripDateSlotStatus Status);
