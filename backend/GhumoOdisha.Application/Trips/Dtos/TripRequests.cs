using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Trips.Dtos;

public record CreateTripRequest(
    string Title,
    string Description,
    decimal AmountPerPerson,
    bool IncludesBreakfast,
    bool IncludesLunch,
    bool IncludesDinner,
    bool IncludesStay,
    bool IncludesCoordinator);

public record UpdateTripRequest(
    string Title,
    string Description,
    decimal AmountPerPerson,
    bool IncludesBreakfast,
    bool IncludesLunch,
    bool IncludesDinner,
    bool IncludesStay,
    bool IncludesCoordinator,
    TripStatus Status);
