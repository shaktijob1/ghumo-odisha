using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Holidays;

public interface IHolidayService
{
    /// <summary>Active destinations that have at least one active package — the Holidays tab's destination row.</summary>
    Task<IReadOnlyList<HolidayDestinationDto>> GetDestinationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Active packages, optionally for one destination — the Holidays tab's package list.</summary>
    Task<IReadOnlyList<HolidayPackageDto>> GetPackagesAsync(int? holidayDestinationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminHolidayDestinationDto>> GetAdminDestinationsAsync(CancellationToken cancellationToken = default);
    Task<int> CreateDestinationAsync(SaveHolidayDestinationRequest request, CancellationToken cancellationToken = default);
    Task UpdateDestinationAsync(int id, SaveHolidayDestinationRequest request, CancellationToken cancellationToken = default);
    Task DeleteDestinationAsync(int id, CancellationToken cancellationToken = default);
    Task SetDestinationPhotoAsync(int id, UploadedImage image, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminHolidayPackageDto>> GetAdminPackagesAsync(CancellationToken cancellationToken = default);
    Task<int> CreatePackageAsync(SaveHolidayPackageRequest request, CancellationToken cancellationToken = default);
    Task UpdatePackageAsync(int id, SaveHolidayPackageRequest request, CancellationToken cancellationToken = default);
    Task DeletePackageAsync(int id, CancellationToken cancellationToken = default);
    Task SetPackagePhotoAsync(int id, UploadedImage image, CancellationToken cancellationToken = default);
}

/// <summary>
/// Holiday destinations and custom holiday packages for the home page's Holidays tab. Kept apart
/// from trips: no seats, dates or online booking — customers enquire on WhatsApp.
/// </summary>
public class HolidayService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : IHolidayService
{
    private const string ImageCategory = "holidays";

    // ---------- Public ----------

    public async Task<IReadOnlyList<HolidayDestinationDto>> GetDestinationsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.HolidayDestinations.AsNoTracking()
            .Where(d => d.IsActive && d.Packages.Any(p => p.IsActive))
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
            .Select(d => new
            {
                d.HolidayDestinationId,
                d.Name,
                d.Tagline,
                d.CoverImageUrl,
                Count = d.Packages.Count(p => p.IsActive),
                Starting = d.Packages.Where(p => p.IsActive).Min(p => p.PriceFrom),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(r => new HolidayDestinationDto(r.HolidayDestinationId, r.Name, r.Tagline, r.CoverImageUrl, r.Count, r.Starting)).ToList();
    }

    public async Task<IReadOnlyList<HolidayPackageDto>> GetPackagesAsync(int? holidayDestinationId, CancellationToken cancellationToken = default)
    {
        var query = db.HolidayPackages.AsNoTracking()
            .Include(p => p.HolidayDestination)
            // A package whose destination is switched off is hidden with it.
            .Where(p => p.IsActive && (p.HolidayDestination == null || p.HolidayDestination.IsActive));
        if (holidayDestinationId.HasValue)
        {
            query = query.Where(p => p.HolidayDestinationId == holidayDestinationId.Value);
        }

        var packages = await query.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Title).ToListAsync(cancellationToken);
        return packages.Select(p => new HolidayPackageDto(p.HolidayPackageId, p.Title, p.Summary, p.Days, p.Nights,
            DurationLabel(p.Days, p.Nights), p.PriceFrom, SplitLines(p.Highlights), p.CoverImageUrl,
            p.HolidayDestinationId, p.HolidayDestination?.Name)).ToList();
    }

    // ---------- Admin: destinations ----------

    public async Task<IReadOnlyList<AdminHolidayDestinationDto>> GetAdminDestinationsAsync(CancellationToken cancellationToken = default) =>
        await db.HolidayDestinations.AsNoTracking()
            .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
            .Select(d => new AdminHolidayDestinationDto(d.HolidayDestinationId, d.Name, d.Tagline, d.CoverImageUrl, d.DisplayOrder, d.IsActive, d.Packages.Count))
            .ToListAsync(cancellationToken);

    public async Task<int> CreateDestinationAsync(SaveHolidayDestinationRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(name, null, cancellationToken);
        var now = DateTime.UtcNow;
        var destination = new HolidayDestination
        {
            Name = name,
            Tagline = Clean(request.Tagline),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.HolidayDestinations.Add(destination);
        await db.SaveChangesAsync(cancellationToken);
        return destination.HolidayDestinationId;
    }

    public async Task UpdateDestinationAsync(int id, SaveHolidayDestinationRequest request, CancellationToken cancellationToken = default)
    {
        var destination = await FindDestinationAsync(id, cancellationToken);
        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(name, id, cancellationToken);
        destination.Name = name;
        destination.Tagline = Clean(request.Tagline);
        destination.DisplayOrder = request.DisplayOrder;
        destination.IsActive = request.IsActive;
        destination.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Its packages stay, just no longer linked to a destination.</summary>
    public async Task DeleteDestinationAsync(int id, CancellationToken cancellationToken = default)
    {
        var destination = await FindDestinationAsync(id, cancellationToken);
        var photo = destination.CoverImageUrl;
        await db.HolidayPackages.Where(p => p.HolidayDestinationId == id).ExecuteUpdateAsync(u => u.SetProperty(p => p.HolidayDestinationId, (int?)null), cancellationToken);
        db.HolidayDestinations.Remove(destination);
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(photo)) imageStorage.Delete(photo);
    }

    public async Task SetDestinationPhotoAsync(int id, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var destination = await FindDestinationAsync(id, cancellationToken);
        UploadedImageRules.Validate(image);
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, ImageCategory, cancellationToken);
        var old = destination.CoverImageUrl;
        destination.CoverImageUrl = url;
        destination.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(old)) imageStorage.Delete(old);
    }

    // ---------- Admin: packages ----------

    public async Task<IReadOnlyList<AdminHolidayPackageDto>> GetAdminPackagesAsync(CancellationToken cancellationToken = default)
    {
        var packages = await db.HolidayPackages.AsNoTracking()
            .Include(p => p.HolidayDestination)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Title)
            .ToListAsync(cancellationToken);
        return packages.Select(p => new AdminHolidayPackageDto(p.HolidayPackageId, p.Title, p.Summary, p.Days, p.Nights,
            DurationLabel(p.Days, p.Nights), p.PriceFrom, SplitLines(p.Highlights), p.CoverImageUrl,
            p.HolidayDestinationId, p.HolidayDestination?.Name, p.DisplayOrder, p.IsActive)).ToList();
    }

    public async Task<int> CreatePackageAsync(SaveHolidayPackageRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureDestinationExistsAsync(request.HolidayDestinationId, cancellationToken);
        var now = DateTime.UtcNow;
        var package = new HolidayPackage { CreatedAt = now };
        Apply(package, request, now);
        db.HolidayPackages.Add(package);
        await db.SaveChangesAsync(cancellationToken);
        return package.HolidayPackageId;
    }

    public async Task UpdatePackageAsync(int id, SaveHolidayPackageRequest request, CancellationToken cancellationToken = default)
    {
        var package = await FindPackageAsync(id, cancellationToken);
        await EnsureDestinationExistsAsync(request.HolidayDestinationId, cancellationToken);
        Apply(package, request, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePackageAsync(int id, CancellationToken cancellationToken = default)
    {
        var package = await FindPackageAsync(id, cancellationToken);
        var photo = package.CoverImageUrl;
        db.HolidayPackages.Remove(package);
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(photo)) imageStorage.Delete(photo);
    }

    public async Task SetPackagePhotoAsync(int id, UploadedImage image, CancellationToken cancellationToken = default)
    {
        var package = await FindPackageAsync(id, cancellationToken);
        UploadedImageRules.Validate(image);
        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, ImageCategory, cancellationToken);
        var old = package.CoverImageUrl;
        package.CoverImageUrl = url;
        package.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(old)) imageStorage.Delete(old);
    }

    // ---------- Helpers ----------

    /// <summary>"4 Days / 3 Nights"; a day trip reads "1 Day".</summary>
    public static string DurationLabel(int days, int nights)
    {
        var d = $"{days} {(days == 1 ? "Day" : "Days")}";
        return nights <= 0 ? d : $"{d} / {nights} {(nights == 1 ? "Night" : "Nights")}";
    }

    private static void Apply(HolidayPackage package, SaveHolidayPackageRequest request, DateTime now)
    {
        package.Title = request.Title.Trim();
        package.Summary = Clean(request.Summary);
        package.Days = request.Days;
        package.Nights = request.Nights;
        package.PriceFrom = request.PriceFrom;
        package.Highlights = JoinLines(request.Highlights);
        package.HolidayDestinationId = request.HolidayDestinationId;
        package.DisplayOrder = request.DisplayOrder;
        package.IsActive = request.IsActive;
        package.UpdatedAt = now;
    }

    private async Task EnsureUniqueNameAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        if (await db.HolidayDestinations.AnyAsync(d => d.Name == name && d.HolidayDestinationId != exceptId, cancellationToken))
        {
            throw new ConflictException($"A holiday destination called \"{name}\" already exists.");
        }
    }

    private async Task EnsureDestinationExistsAsync(int? id, CancellationToken cancellationToken)
    {
        if (id.HasValue && !await db.HolidayDestinations.AnyAsync(d => d.HolidayDestinationId == id.Value, cancellationToken))
        {
            throw new ValidationAppException(["Choose one of the holiday destinations."]);
        }
    }

    private async Task<HolidayDestination> FindDestinationAsync(int id, CancellationToken cancellationToken) =>
        await db.HolidayDestinations.FirstOrDefaultAsync(d => d.HolidayDestinationId == id, cancellationToken)
            ?? throw new NotFoundException("Holiday destination not found.");

    private async Task<HolidayPackage> FindPackageAsync(int id, CancellationToken cancellationToken) =>
        await db.HolidayPackages.FirstOrDefaultAsync(p => p.HolidayPackageId == id, cancellationToken)
            ?? throw new NotFoundException("Holiday package not found.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? JoinLines(IReadOnlyList<string>? lines)
    {
        var clean = (lines ?? []).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return clean.Count == 0 ? null : string.Join('\n', clean);
    }

    private static IReadOnlyList<string> SplitLines(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? [] : stored.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
