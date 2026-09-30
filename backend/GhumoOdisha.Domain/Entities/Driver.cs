using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A driver who rents out their own car through Ghumo Odisha. Signs in with WhatsApp OTP or Google,
/// like a customer, but is a separate account with the "Driver" role. Receives bookings only once an
/// admin approves them (and their car and its pricing).
/// </summary>
public class Driver
{
    public int DriverId { get; set; }
    public string Name { get; set; } = null!;
    // Null for drivers who signed up with Google and haven't verified a WhatsApp number yet.
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? GoogleSubject { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    /// <summary>Where the driver starts from (home / stand). The km from here to the customer's pickup are
    /// added to the fare. A car can't be quoted until its driver has one.</summary>
    public double? BaseLatitude { get; set; }
    public double? BaseLongitude { get; set; }
    public string? BaseLocationLabel { get; set; }
    public string? DrivingLicenceNumber { get; set; }
    public DateOnly? LicenceExpiryDate { get; set; }
    public int? ExperienceYears { get; set; }
    public string? ProfilePhotoUrl { get; set; }

    public DriverStatus Status { get; set; }
    /// <summary>Why the admin rejected / suspended the driver — shown to the driver.</summary>
    public string? StatusReason { get; set; }
    /// <summary>Set when the driver sends their completed profile for review.</summary>
    public DateTime? SubmittedForReviewAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    /// <summary>Set when an admin added this driver (e.g. signed up in person). Such a driver can be approved
    /// directly, without first completing and submitting their profile.</summary>
    public int? AddedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Car> Cars { get; set; } = new List<Car>();
    public ICollection<DriverDocument> Documents { get; set; } = new List<DriverDocument>();
    public ICollection<DriverRefreshToken> RefreshTokens { get; set; } = new List<DriverRefreshToken>();
}
