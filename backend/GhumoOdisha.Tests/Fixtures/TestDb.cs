using GhumoOdisha.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>
/// Connects to the same local MySQL instance the app uses in Development (matches
/// appsettings.Development.json). This project has no CI/mocking infra — every prior
/// phase was verified against a real database, and these tests follow that pattern.
/// </summary>
public static class TestDb
{
    private const string ConnectionString = "server=localhost;port=3306;database=GhumoOdisha;user=shaktijob;password=1111;";

    public static GhumoOdishaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<GhumoOdishaDbContext>()
            .UseMySql(ConnectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        return new GhumoOdishaDbContext(options);
    }

    /// <summary>Random 10-digit Indian mobile number so parallel/repeated test runs never collide.</summary>
    public static string RandomPhoneNumber()
    {
        var random = new Random();
        var digits = string.Concat(Enumerable.Range(0, 9).Select(_ => random.Next(0, 10)));
        return $"{random.Next(6, 10)}{digits}";
    }
}
