namespace GhumoOdisha.Application.Common;

public interface IImageStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, string subFolder, CancellationToken cancellationToken = default);

    void Delete(string relativeUrl);
}
