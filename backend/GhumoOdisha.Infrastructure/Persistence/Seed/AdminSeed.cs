namespace GhumoOdisha.Infrastructure.Persistence.Seed;

/// <summary>First admin account created on startup when no admin exists yet (see "Seed" in appsettings).</summary>
public sealed record AdminSeed(string Username, string Password, string Email);
