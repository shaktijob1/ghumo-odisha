using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// A message the admin sends by hand from WhatsApp Web. {name}, {trip}, {date} etc. are filled in
/// for the chosen customer and trip before WhatsApp opens. Not a WhatsApp Business API template.
/// </summary>
public class CrmWhatsAppTemplate
{
    public int CrmWhatsAppTemplateId { get; set; }
    public string Name { get; set; } = null!;
    public CrmTemplateCategory Category { get; set; }
    public string Body { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
