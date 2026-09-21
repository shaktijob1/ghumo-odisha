using System.Net;

namespace GhumoOdisha.Application.Exceptions;

public class AccountLockedException(int retryAfterMinutes)
    : AppException($"Too many attempts. Try again in {retryAfterMinutes} minutes.", HttpStatusCode.Locked)
{
    public int RetryAfterMinutes { get; } = retryAfterMinutes;
}
