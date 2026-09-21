namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Defined for the future PIN-reset flow. Deliberately left unregistered in DI until
/// that flow ships, so the auth service shape doesn't change when it does.
/// </summary>
public interface IOtpSender
{
    Task SendAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default);
}
