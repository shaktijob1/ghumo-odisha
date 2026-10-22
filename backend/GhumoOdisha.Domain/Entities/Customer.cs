using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

public class Customer
{
    public int CustomerId { get; set; }
    public string Name { get; set; } = null!;
    // Null for customers who signed up with Google and haven't added a mobile number yet —
    // WhatsApp notices are skipped for them.
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    // True only once Google has vouched for Email. Google account matching only uses a verified
    // email, so typing someone else's address into a profile can't take over their account.
    public bool EmailVerified { get; set; }

    // Google's stable account id ("sub" claim), set when the customer signs in with / links Google.
    public string? GoogleSubject { get; set; }
    public bool IsVerified { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // ---- CRM (Admin → CRM). Website sign-ups are CRM customers too, with LeadSource Website. ----
    public string? AlternatePhoneNumber { get; set; }
    public bool WhatsAppAvailable { get; set; } = true;
    public string? City { get; set; }
    public LeadSource LeadSource { get; set; } = LeadSource.Website;
    public CrmStatus CrmStatus { get; set; } = CrmStatus.NewLead;
    /// <summary>Standing notes about the customer; dated notes are <see cref="CrmNotes"/>.</summary>
    public string? CrmSummary { get; set; }
    public DateTime? LastContactAt { get; set; }
    public int? AssignedAdminUserId { get; set; }

    public AdminUser? AssignedAdminUser { get; set; }
    public ICollection<CustomerTripInterest> TripInterests { get; set; } = new List<CustomerTripInterest>();
    public ICollection<CrmFollowUp> CrmFollowUps { get; set; } = new List<CrmFollowUp>();
    public ICollection<CrmCommunication> CrmCommunications { get; set; } = new List<CrmCommunication>();
    public ICollection<CrmNote> CrmNotes { get; set; } = new List<CrmNote>();
    public ICollection<CrmStatusChange> CrmStatusChanges { get; set; } = new List<CrmStatusChange>();

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<CustomerRefreshToken> RefreshTokens { get; set; } = new List<CustomerRefreshToken>();
}
