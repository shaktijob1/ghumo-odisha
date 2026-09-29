using System.Security.Cryptography;
using System.Text;

namespace GhumoOdisha.Application.Auth;

/// <summary>Opaque refresh tokens: random, URL-safe, and stored only as a SHA-256 hash.</summary>
public static class RefreshTokens
{
    public static string NewPlain() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
