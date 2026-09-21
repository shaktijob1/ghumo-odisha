using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class NotFoundException(string message) : AppException(message, HttpStatusCode.NotFound);
