namespace GhumoOdisha.Application.Trips.Dtos;

public record AddTripHighlightRequest(string PlaceName, string Description);

public record UpdateTripHighlightRequest(string PlaceName, string Description, int DisplayOrder);
