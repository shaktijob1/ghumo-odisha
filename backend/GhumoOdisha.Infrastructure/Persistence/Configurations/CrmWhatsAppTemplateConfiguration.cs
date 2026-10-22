using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CrmWhatsAppTemplateConfiguration : IEntityTypeConfiguration<CrmWhatsAppTemplate>
{
    public void Configure(EntityTypeBuilder<CrmWhatsAppTemplate> builder)
    {
        builder.ToTable("CrmWhatsAppTemplates");

        builder.HasKey(t => t.CrmWhatsAppTemplateId);

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Category).HasConversion<int>();
        builder.Property(t => t.Body).HasMaxLength(2000).IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(t => t.Name).IsUnique();
        builder.HasIndex(t => new { t.IsActive, t.Category });
    }
}
