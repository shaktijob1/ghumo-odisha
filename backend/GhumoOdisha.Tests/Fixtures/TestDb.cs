using GhumoOdisha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>
/// Connects to the local MySQL instance the app uses in Development, but to its own
/// <c>GhumoOdisha_Test</c> database — the tests create thousands of trips, customers and bookings,
/// which must never land in the database the running site reads. Migrated once per test run.
/// This project has no CI/mocking infra — every phase was verified against a real database.
/// </summary>
public static class TestDb
{
    private const string ConnectionString = "server=localhost;port=3306;database=GhumoOdisha_Test;user=shaktijob;password=1111;";

    private static readonly Lazy<bool> Migrated = new(() =>
    {
        using var context = new GhumoOdishaDbContext(Options());
        context.Database.Migrate();
        return true;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static GhumoOdishaDbContext CreateContext()
    {
        _ = Migrated.Value;
        return new GhumoOdishaDbContext(Options());
    }

    private static DbContextOptions<GhumoOdishaDbContext> Options() =>
        new DbContextOptionsBuilder<GhumoOdishaDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

    /// <summary>Random 10-digit Indian mobile number so parallel/repeated test runs never collide.</summary>
    public static string RandomPhoneNumber()
    {
        var random = new Random();
        var digits = string.Concat(Enumerable.Range(0, 9).Select(_ => random.Next(0, 10)));
        return $"{random.Next(6, 10)}{digits}";
    }
}
