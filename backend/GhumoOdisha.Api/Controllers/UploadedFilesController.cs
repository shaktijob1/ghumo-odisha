using GhumoOdisha.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Api.Controllers;

/// <summary>
/// Serves the publicly-viewable uploaded images (trip/room/vehicle/highlight/organizer/destination
/// photos) from persistent storage. This is the only way those files reach a browser — the storage
/// root sits outside wwwroot and is deliberately never exposed via UseStaticFiles, so this endpoint
/// is what stays "controlled": it whitelists the category, rejects anything that would resolve
/// outside that category's folder, and 404s rather than leaking which paths exist.
///
/// Route shape matches the "/uploads/{category}/{file}" URLs already stored in the database
/// (see IImageStorage.SaveAsync), so no data migration or frontend change was needed to add this.
///
/// This must never be used for private files (customer documents, invoices) — those require an
/// authenticated/authorized endpoint of their own, not a public route like this one.
/// </summary>
[ApiController]
public class UploadedFilesController(IOptions<ImageStorageOptions> options) : ControllerBase
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "trips", "highlights", "rooms", "vehicles", "organizer", "destinations", "itineraries", "hero"
    };

    private static readonly Dictionary<string, string> ContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".jfif"] = "image/jpeg",
        [".png"] = "image/png",
        [".pdf"] = "application/pdf",
    };

    [HttpGet("uploads/{category}/{fileName}")]
    public IActionResult Get(string category, string fileName)
    {
        if (!AllowedCategories.Contains(category) ||
            !ContentTypesByExtension.TryGetValue(Path.GetExtension(fileName), out var contentType))
        {
            return NotFound();
        }

        var categoryRoot = Path.GetFullPath(Path.Combine(options.Value.BasePath, category));
        var fullPath = Path.GetFullPath(Path.Combine(categoryRoot, fileName));

        // Defense in depth against path traversal: Path.GetFullPath already collapses ".." segments,
        // this just confirms the resolved path never escaped the category folder before touching disk.
        if (!fullPath.StartsWith(categoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, contentType);
    }
}
