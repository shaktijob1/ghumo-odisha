namespace GhumoOdisha.Domain.Enums;

/// <summary>Where a CRM lead came from — used for lead-source reporting and conversion rates.</summary>
public enum LeadSource
{
    Instagram = 0,
    Facebook = 1,
    WhatsApp = 2,
    /// <summary>Default for everyone who signed up or booked on the website.</summary>
    Website = 3,
    Referral = 4,
    Google = 5,
    WalkIn = 6,
    Phone = 7,
    Other = 8
}
