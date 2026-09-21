using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class ItineraryPointConfiguration : IEntityTypeConfiguration<ItineraryPoint>
{
    public void Configure(EntityTypeBuilder<ItineraryPoint> builder)
    {
        builder.ToTable("ItineraryPoints");

        builder.HasKey(p => p.ItineraryPointId);

        builder.Property(p => p.Time)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasColumnType("text");

        builder.HasIndex(p => new { p.ItineraryDayId, p.DisplayOrder });
    }
}
