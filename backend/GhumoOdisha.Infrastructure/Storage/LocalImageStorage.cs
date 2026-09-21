using GhumoOdisha.Application.Common;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Infrastructure.Storage;

public class LocalImageStorage(IOptions<ImageStorageOptions> options) : IImageStorage
{
    private readonly ImageStorageOptions _options = options.Value;

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, string subFolder, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{extension}";

        var folderPath = Path.Combine(_options.BasePath, subFolder);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, uniqueName);
        await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        return $"{_options.PublicUrlPrefix}/{subFolder}/{uniqueName}";
    }

    public void Delete(string relativeUrl)
    {
        var prefix = $"{_options.PublicUrlPrefix}/";
        if (!relativeUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = relativeUrl[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.Combine(_options.BasePath, relativePath);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
