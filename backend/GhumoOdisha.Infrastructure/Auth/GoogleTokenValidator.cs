using Google.Apis.Auth;
using GhumoOdisha.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Auth;

/// <summary>
/// Verifies Google Identity Services ID tokens with Google's published signing keys (fetched and
/// cached by Google.Apis.Auth). The audience must be our OAuth client id, so a token minted for
/// some other site can't be replayed here.
/// </summary>
public class GoogleTokenValidator(IOptions<GoogleAuthOptions> options, ILogger<GoogleTokenValidator> logger) : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            logger.LogError("GoogleAuth:ClientId is not configured — Google sign-in is unavailable.");
            return null;
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId]
            });

            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning("Rejected Google ID token: {Reason}", ex.Message);
            return null;
        }
    }
}
