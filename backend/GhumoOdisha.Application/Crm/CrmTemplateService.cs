using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

public interface ICrmTemplateService
{
    Task<IReadOnlyList<CrmTemplateDto>> GetTemplatesAsync(bool activeOnly, CrmTemplateCategory? category, CancellationToken cancellationToken = default);
    Task<CrmTemplateDto> GetTemplateAsync(int templateId, CancellationToken cancellationToken = default);
    Task<CrmTemplateDto> CreateTemplateAsync(SaveCrmTemplateRequest request, CancellationToken cancellationToken = default);
    Task<CrmTemplateDto> UpdateTemplateAsync(int templateId, SaveCrmTemplateRequest request, CancellationToken cancellationToken = default);
    Task<CrmTemplateDto> SetActiveAsync(int templateId, bool isActive, CancellationToken cancellationToken = default);
    Task<CrmTemplateDto> DuplicateTemplateAsync(int templateId, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(int templateId, CancellationToken cancellationToken = default);
}

/// <summary>WhatsApp message templates the admin picks from before opening WhatsApp Web. Plain text with {placeholders}.</summary>
public class CrmTemplateService(IGhumoOdishaDbContext db) : ICrmTemplateService
{
    public async Task<IReadOnlyList<CrmTemplateDto>> GetTemplatesAsync(bool activeOnly, CrmTemplateCategory? category, CancellationToken cancellationToken = default)
    {
        var query = db.CrmWhatsAppTemplates.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(t => t.IsActive);
        }

        if (category.HasValue)
        {
            query = query.Where(t => t.Category == category.Value);
        }

        return await query.OrderBy(t => t.Category).ThenBy(t => t.Name).Select(t => ToDto(t)).ToListAsync(cancellationToken);
    }

    public async Task<CrmTemplateDto> GetTemplateAsync(int templateId, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(templateId, cancellationToken));

    public async Task<CrmTemplateDto> CreateTemplateAsync(SaveCrmTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        await EnsureNameFreeAsync(name, null, cancellationToken);
        EnsurePlaceholdersKnown(request.Body);

        var now = DateTime.UtcNow;
        var template = new CrmWhatsAppTemplate
        {
            Name = name,
            Category = request.Category,
            Body = NormalizeBody(request.Body),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CrmWhatsAppTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<CrmTemplateDto> UpdateTemplateAsync(int templateId, SaveCrmTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var template = await LoadAsync(templateId, cancellationToken);
        var name = request.Name.Trim();
        await EnsureNameFreeAsync(name, templateId, cancellationToken);
        EnsurePlaceholdersKnown(request.Body);

        template.Name = name;
        template.Category = request.Category;
        template.Body = NormalizeBody(request.Body);
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<CrmTemplateDto> SetActiveAsync(int templateId, bool isActive, CancellationToken cancellationToken = default)
    {
        var template = await LoadAsync(templateId, cancellationToken);
        template.IsActive = isActive;
        template.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    public async Task<CrmTemplateDto> DuplicateTemplateAsync(int templateId, CancellationToken cancellationToken = default)
    {
        var source = await LoadAsync(templateId, cancellationToken);

        // "Follow-up (copy)", then "(copy 2)", … — names are unique.
        var baseName = source.Name.Length > 85 ? source.Name[..85] : source.Name;
        var name = $"{baseName} (copy)";
        for (var n = 2; await db.CrmWhatsAppTemplates.AnyAsync(t => t.Name == name, cancellationToken); n++)
        {
            name = $"{baseName} (copy {n})";
        }

        var now = DateTime.UtcNow;
        var copy = new CrmWhatsAppTemplate
        {
            Name = name,
            Category = source.Category,
            Body = source.Body,
            // A copy starts switched off so a half-edited template isn't offered by accident.
            IsActive = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CrmWhatsAppTemplates.Add(copy);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(copy);
    }

    public async Task DeleteTemplateAsync(int templateId, CancellationToken cancellationToken = default)
    {
        var template = await LoadAsync(templateId, cancellationToken);
        // Messages already sent keep the template's name (stored on the log), so deleting is safe.
        db.CrmWhatsAppTemplates.Remove(template);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CrmWhatsAppTemplate> LoadAsync(int templateId, CancellationToken cancellationToken) =>
        await db.CrmWhatsAppTemplates.FirstOrDefaultAsync(t => t.CrmWhatsAppTemplateId == templateId, cancellationToken)
        ?? throw new NotFoundException("Template not found.");

    private async Task EnsureNameFreeAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        if (await db.CrmWhatsAppTemplates.AnyAsync(t => t.Name == name && (exceptId == null || t.CrmWhatsAppTemplateId != exceptId), cancellationToken))
        {
            throw new ConflictException("A template with this name already exists.");
        }
    }

    private static void EnsurePlaceholdersKnown(string body)
    {
        var unknown = CrmMessageRenderer.UnknownPlaceholders(body);
        if (unknown.Count > 0)
        {
            throw new ValidationAppException([
                $"Unknown placeholder(s): {string.Join(", ", unknown.Select(u => "{" + u + "}"))}. " +
                $"Use only: {string.Join(" ", CrmMessageRenderer.Placeholders.Select(p => "{" + p + "}"))}."
            ]);
        }
    }

    /// <summary>One kind of line break (\n), so templates read the same wherever they were typed.</summary>
    private static string NormalizeBody(string body) => body.Replace("\r\n", "\n").Trim();

    private static CrmTemplateDto ToDto(CrmWhatsAppTemplate t) =>
        new(t.CrmWhatsAppTemplateId, t.Name, t.Category, t.Body, t.IsActive, t.CreatedAt, t.UpdatedAt);
}
