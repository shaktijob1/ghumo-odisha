namespace GhumoOdisha.Application.Trips.Dtos;

public record AddPickupPointRequest(string Location, string Time);

public record UpdatePickupPointRequest(string Location, string Time, int DisplayOrder);
