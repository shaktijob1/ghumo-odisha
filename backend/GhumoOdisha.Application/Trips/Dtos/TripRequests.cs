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
    bool IncludesCoordinator,
    IReadOnlyList<int>? DestinationIds = null,
    bool IncludesAcVehicle = false,
    bool IncludesPushbackVehicle = false,
    bool IncludesCamping = false,
    bool IncludesBonfire = false,
    bool IncludesMusicalNight = false,
    bool IncludesSwimmingPool = false);

public record UpdateTripRequest(
    string Title,
    string Description,
    decimal AmountPerPerson,
    bool IncludesBreakfast,
    bool IncludesLunch,
    bool IncludesDinner,
    bool IncludesStay,
    bool IncludesCoordinator,
    TripStatus Status,
    IReadOnlyList<int>? DestinationIds = null,
    bool IncludesAcVehicle = false,
    bool IncludesPushbackVehicle = false,
    bool IncludesCamping = false,
    bool IncludesBonfire = false,
    bool IncludesMusicalNight = false,
    bool IncludesSwimmingPool = false);
