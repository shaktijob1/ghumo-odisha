namespace GhumoOdisha.Application.Common;

public interface IImageStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, string subFolder, CancellationToken cancellationToken = default);

    void Delete(string relativeUrl);

    /// <summary>
    /// Opens a stored file for streaming through an authorized endpoint (private documents). Null when
    /// the file is missing or the URL would resolve outside the storage folder.
    /// </summary>
    Stream? OpenRead(string relativeUrl);
}
