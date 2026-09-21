using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class TooManyAttemptsException()
    : AppException("Too many attempts. Please try again in a minute.", HttpStatusCode.TooManyRequests);
