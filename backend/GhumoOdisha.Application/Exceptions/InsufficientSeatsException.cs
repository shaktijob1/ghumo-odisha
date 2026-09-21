using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class InsufficientSeatsException(int availableSeats)
    : AppException($"Only {availableSeats} seat(s) are currently available.", HttpStatusCode.Conflict)
{
    public int AvailableSeats { get; } = availableSeats;
}
