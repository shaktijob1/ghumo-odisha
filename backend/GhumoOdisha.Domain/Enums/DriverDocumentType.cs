namespace GhumoOdisha.Domain.Enums;

/// <summary>Verification documents — visible to the uploading driver and admins only, never to customers.</summary>
public enum DriverDocumentType
{
    DrivingLicence = 0,
    RegistrationCertificate = 1,
    Insurance = 2,
    Permit = 3,
    Other = 4
}
