namespace GhumoOdisha.Infrastructure.Storage;

public class ImageStorageOptions
{
    /// <summary>Absolute filesystem path to the web root's uploads directory (e.g. wwwroot/uploads).</summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>Public URL prefix the saved files are served under (e.g. /uploads).</summary>
    public string PublicUrlPrefix { get; set; } = "/uploads";
}
