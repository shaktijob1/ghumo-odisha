namespace GhumoOdisha.Infrastructure.Storage;

/// <summary>
/// Root of the persistent, deployment-independent storage tree. Everything the app writes at
/// runtime (uploaded photos today; documents/invoices if that's ever needed later) lives under
/// this path, kept deliberately outside the deployed application directory so a redeploy can
/// never touch it. Set via "Storage:RootPath" in appsettings, or the Storage__RootPath
/// environment variable in production — never hardcoded and never committed for production.
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; set; } = string.Empty;
}
