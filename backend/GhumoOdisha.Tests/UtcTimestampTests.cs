using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Tests;

public class UtcTimestampTests
{
    [Fact]
    public async Task TimestampsReadFromDatabase_AreMarkedUtc_SoApiSendsZ()
    {
        await using var db = TestDb.CreateContext();
        var now = DateTime.UtcNow;
        var customer = new Customer { Name = "Utc Check", PhoneNumber = TestDb.RandomPhoneNumber(), CreatedAt = now, UpdatedAt = now, LastLoginAt = now };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        await using var fresh = TestDb.CreateContext();
        var loaded = await fresh.Customers.AsNoTracking().SingleAsync(c => c.CustomerId == customer.CustomerId);

        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, loaded.LastLoginAt!.Value.Kind);
        Assert.Equal(now, loaded.CreatedAt, TimeSpan.FromMilliseconds(1)); // same instant, not shifted
        Assert.EndsWith("Z", System.Text.Json.JsonSerializer.Serialize(loaded.CreatedAt).Trim('"'));
    }
}
