using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CrmCommunicationConfiguration : IEntityTypeConfiguration<CrmCommunication>
{
    public void Configure(EntityTypeBuilder<CrmCommunication> builder)
    {
        builder.ToTable("CrmCommunications");

        builder.HasKey(c => c.CrmCommunicationId);

        builder.Property(c => c.Channel).HasConversion<int>();
        builder.Property(c => c.Status).HasConversion<int>();
        builder.Property(c => c.Action).HasMaxLength(200).IsRequired();
        builder.Property(c => c.TemplateName).HasMaxLength(100);
        builder.Property(c => c.MessagePreview).HasMaxLength(4000);
        builder.Property(c => c.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.SentConfirmedAt).HasColumnType("datetime(6)");

        builder.HasIndex(c => new { c.CustomerId, c.CreatedAt });
        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => c.CustomerTripInterestId);

        builder.HasOne(c => c.Customer).WithMany(x => x.CrmCommunications).HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(c => c.TripInterest).WithMany().HasForeignKey(c => c.CustomerTripInterestId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(c => c.Template).WithMany().HasForeignKey(c => c.CrmWhatsAppTemplateId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(c => c.AdminUser).WithMany().HasForeignKey(c => c.AdminUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
