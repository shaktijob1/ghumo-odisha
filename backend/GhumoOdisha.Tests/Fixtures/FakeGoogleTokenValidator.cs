using GhumoOdisha.Application.Auth;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>
/// Stands in for Google's signature check. Tests register a token string → identity mapping;
/// any other token is treated as invalid, exactly like a forged or expired one.
/// </summary>
public class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly Dictionary<string, GoogleIdentity> _tokens = new();

    /// <summary>Registers a fresh Google identity and returns the "ID token" that maps to it.</summary>
    public string Issue(string? email = null, string? name = "Google User", bool emailVerified = true, string? subject = null)
    {
        var sub = subject ?? Random.Shared.NextInt64(100_000_000_000, 999_999_999_999).ToString();
        var token = $"token-{Guid.NewGuid():N}";
        _tokens[token] = new GoogleIdentity(sub, email ?? $"user{sub}@gmail.com", emailVerified, name);
        return token;
    }

    public GoogleIdentity IdentityFor(string token) => _tokens[token];

    public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokens.TryGetValue(idToken, out var identity) ? identity : null);
}
