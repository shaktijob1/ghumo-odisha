using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Holidays;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

public class HolidayAndOrganizerTests
{
    [Fact]
    public async Task HolidayDestinations_ShowOnlyWithAnActivePackage_AndPackagesFilterByDestination()
    {
        await using var db = TestDb.CreateContext();
        var tag = Guid.NewGuid().ToString("N")[..8];
        var service = new HolidayService(db, null!);

        var goa = await service.CreateDestinationAsync(new SaveHolidayDestinationRequest($"Goa {tag}", "Beaches", 0, true));
        var empty = await service.CreateDestinationAsync(new SaveHolidayDestinationRequest($"Empty {tag}", null, 1, true));
        var hidden = await service.CreateDestinationAsync(new SaveHolidayDestinationRequest($"Hidden {tag}", null, 2, false));

        var p1 = await service.CreatePackageAsync(new SaveHolidayPackageRequest($"Goa beach {tag}", "Sun and sand", 4, 3, 12999m, ["North Goa", " ", "Baga Beach", "north goa"], goa, 0, true));
        await service.CreatePackageAsync(new SaveHolidayPackageRequest($"Goa luxury {tag}", null, 5, 4, 24999m, null, goa, 1, true));
        await service.CreatePackageAsync(new SaveHolidayPackageRequest($"Goa draft {tag}", null, 3, 2, 999m, null, goa, 2, false));
        await service.CreatePackageAsync(new SaveHolidayPackageRequest($"Hidden pkg {tag}", null, 2, 1, null, null, hidden, 0, true));

        try
        {
            var destinations = (await service.GetDestinationsAsync()).Where(d => d.Name.EndsWith(tag)).ToList();
            // "Empty" has no package and "Hidden" is switched off: only Goa shows, priced from its cheapest active package.
            var only = Assert.Single(destinations);
            Assert.Equal(goa, only.HolidayDestinationId);
            Assert.Equal(2, only.PackageCount);
            Assert.Equal(12999m, only.StartingPrice);

            var goaPackages = await service.GetPackagesAsync(goa);
            Assert.Equal([$"Goa beach {tag}", $"Goa luxury {tag}"], goaPackages.Select(p => p.Title).ToArray());
            var first = goaPackages[0];
            Assert.Equal("4 Days / 3 Nights", first.DurationLabel);
            Assert.Equal(["North Goa", "Baga Beach"], first.Highlights);

            // A package under a switched-off destination is hidden with it.
            Assert.DoesNotContain(await service.GetPackagesAsync(null), p => p.Title == $"Hidden pkg {tag}");

            await Assert.ThrowsAsync<ConflictException>(() => service.CreateDestinationAsync(new SaveHolidayDestinationRequest($"Goa {tag}", null, 0, true)));
            await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdatePackageAsync(p1, new SaveHolidayPackageRequest("x", null, 1, 0, null, null, 999_999_999, 0, true)));

            // Deleting a destination keeps its packages, unlinked.
            await service.DeleteDestinationAsync(goa);
            db.ChangeTracker.Clear();
            Assert.Null((await db.HolidayPackages.AsNoTracking().SingleAsync(p => p.HolidayPackageId == p1)).HolidayDestinationId);
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.HolidayPackages.Where(p => p.Title.EndsWith(tag)).ExecuteDeleteAsync();
            await db.HolidayDestinations.Where(d => d.Name.EndsWith(tag)).ExecuteDeleteAsync();
            _ = empty;
        }
    }

    [Fact]
    public void HolidayPackageValidator_RejectsMoreNightsThanDays()
    {
        var validator = new SaveHolidayPackageRequestValidator();
        Assert.False(validator.Validate(new SaveHolidayPackageRequest("Trip", null, 2, 3, null, null, null, 0, true)).IsValid);
        Assert.True(validator.Validate(new SaveHolidayPackageRequest("Trip", null, 1, 0, null, null, null, 0, true)).IsValid);
        Assert.Equal("1 Day", HolidayService.DurationLabel(1, 0));
    }

    [Fact]
    public async Task OrganizerCard_UsesSavedTexts_AndFallsBackToAppsettings_WhenCleared()
    {
        await using var db = TestDb.CreateContext();
        var options = Options.Create(new OrganizerContactOptions { Name = "Config Name", Role = "Config Role", Phone = "+911111111111", Email = "config@example.com", WhatsAppNumber = "911111111111" });
        var service = new OrganizerProfileService(db, null!, options);

        await service.UpdateProfileAsync(new UpdateOrganizerProfileRequest("Asha", "Trip lead", null, null, "https://www.instagram.com/ghumo__odisha/", "Hello!", null, null, null));
        var card = await service.GetCardAsync();
        Assert.Equal("Asha", card.Name);
        Assert.Equal("Trip lead", card.Role);
        Assert.Equal("+911111111111", card.Phone);
        Assert.Equal("Hello!", card.Quote);
        Assert.Equal(OrganizerProfileService.DefaultHelpTitle, card.HelpTitle);

        // Blank fields go back to the defaults.
        await service.UpdateProfileAsync(new UpdateOrganizerProfileRequest(" ", null, null, null, null, null, null, null, null));
        var cleared = await service.GetCardAsync();
        Assert.Equal("Config Name", cleared.Name);
        Assert.Equal(OrganizerProfileService.DefaultQuote, cleared.Quote);
        Assert.Null(cleared.InstagramUrl);

        var validator = new UpdateOrganizerProfileRequestValidator();
        Assert.False(validator.Validate(new UpdateOrganizerProfileRequest(null, null, "call me", "not-an-email", "https://evil.example/insta", null, null, null, null)).IsValid);
    }
}
