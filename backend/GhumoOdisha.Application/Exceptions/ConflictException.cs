using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class ConflictException(string message) : AppException(message, HttpStatusCode.Conflict);
