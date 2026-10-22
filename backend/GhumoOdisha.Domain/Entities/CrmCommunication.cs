using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// One contact with a CRM customer: a WhatsApp message the admin opened (and maybe confirmed as
/// sent), or a call/email/other contact they logged. Never records delivery or reads — the CRM
/// can't know those without the WhatsApp API.
/// </summary>
public class CrmCommunication
{
    public int CrmCommunicationId { get; set; }
    public int CustomerId { get; set; }
    public int? CustomerTripInterestId { get; set; }
    public int? CrmWhatsAppTemplateId { get; set; }
    /// <summary>Kept as text so the log still reads right after the template is edited or deleted.</summary>
    public string? TemplateName { get; set; }
    public CrmChannel Channel { get; set; }
    public CrmMessageStatus Status { get; set; }
    /// <summary>Short description: "WhatsApp opened", "Itinerary sent", "Called — no answer".</summary>
    public string Action { get; set; } = null!;
    public string? MessagePreview { get; set; }
    public int? AdminUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>When the admin clicked "Mark as sent".</summary>
    public DateTime? SentConfirmedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public CustomerTripInterest? TripInterest { get; set; }
    public CrmWhatsAppTemplate? Template { get; set; }
    public AdminUser? AdminUser { get; set; }
}
