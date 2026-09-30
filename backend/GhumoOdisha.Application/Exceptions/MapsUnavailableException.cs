using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class MapsUnavailableException(string message = "We couldn't work out the distance right now. Please try again in a moment.")
    : AppException(message, HttpStatusCode.ServiceUnavailable);
