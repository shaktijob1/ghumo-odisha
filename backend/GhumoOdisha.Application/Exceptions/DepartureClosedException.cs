using System.Net;

namespace GhumoOdisha.Application.Exceptions;

/// <summary>The date slot was deactivated or its departure date has passed.</summary>
public class DepartureClosedException(string message = "This departure is no longer open for bookings.")
    : AppException(message, HttpStatusCode.Conflict);
