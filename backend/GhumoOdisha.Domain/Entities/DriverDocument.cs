using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// Licence / RC / insurance scan. Stored outside the public uploads folders and only ever served
/// through an authorized endpoint (the owning driver, or an admin) — never shown to customers.
/// </summary>
public class DriverDocument
{
    public int DriverDocumentId { get; set; }
    public int DriverId { get; set; }
    /// <summary>Set for car documents (RC, insurance, permit); null for the driver's own (licence).</summary>
    public int? CarId { get; set; }
    public DriverDocumentType DocumentType { get; set; }
    public string FileUrl { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public Driver Driver { get; set; } = null!;
    public Car? Car { get; set; }
}
