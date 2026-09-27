namespace GhumoOdisha.Application.Auth;

/// <summary>What a verified Google ID token tells us about the person signing in.</summary>
public record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);

public interface IGoogleTokenValidator
{
    /// <summary>
    /// Verifies a Google Identity Services ID token (signature, issuer, expiry, and that it was
    /// issued for our client id). Returns null when the token is not valid.
    /// </summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
