using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class ValidationAppException(IEnumerable<string> errors)
    : AppException("One or more validation errors occurred.", HttpStatusCode.BadRequest)
{
    public IEnumerable<string> Errors { get; } = errors;
}
