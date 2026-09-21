using GhumoOdisha.Application.Auth;
using Microsoft.AspNetCore.Identity;

namespace GhumoOdisha.Infrastructure.Auth;

/// <summary>
/// Wraps ASP.NET Core's PasswordHasher (PBKDF2, per-hash salt) for both customer PINs
/// and admin passwords. The generic marker type is irrelevant to the hashing algorithm.
/// </summary>
public class PinHasher : IPinHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object HashUser = new();

    public string Hash(string plainText) => _hasher.HashPassword(HashUser, plainText);

    public bool Verify(string hash, string plainText)
        => _hasher.VerifyHashedPassword(HashUser, hash, plainText) != PasswordVerificationResult.Failed;
}
