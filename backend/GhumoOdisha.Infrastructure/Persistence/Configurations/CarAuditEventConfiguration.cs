using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CarAuditEventConfiguration : IEntityTypeConfiguration<CarAuditEvent>
{
    public void Configure(EntityTypeBuilder<CarAuditEvent> builder)
    {
        builder.ToTable("CarAuditEvents");

        builder.HasKey(e => e.CarAuditEventId);

        builder.Property(e => e.EntityType).HasConversion<int>();
        builder.Property(e => e.Action).IsRequired().HasMaxLength(60);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(160);
        builder.Property(e => e.OldValue).HasMaxLength(2000);
        builder.Property(e => e.NewValue).HasMaxLength(2000);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.ActorRole).IsRequired().HasMaxLength(20);
        builder.Property(e => e.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.CreatedAt });
        builder.HasIndex(e => new { e.CarBookingId, e.CreatedAt });
    }
}
