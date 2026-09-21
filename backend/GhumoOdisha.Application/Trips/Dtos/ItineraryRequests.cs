namespace GhumoOdisha.Application.Trips.Dtos;

public record AddItineraryDayRequest(int DayNumber, string Title, string Description);

public record UpdateItineraryDayRequest(int DayNumber, string Title, string Description, int DisplayOrder);

public record AddItineraryPointRequest(string Time, string Description);

public record UpdateItineraryPointRequest(string Time, string Description, int DisplayOrder);
