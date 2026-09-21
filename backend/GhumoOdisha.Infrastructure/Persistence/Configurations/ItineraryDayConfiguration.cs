using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class ItineraryDayConfiguration : IEntityTypeConfiguration<ItineraryDay>
{
    public void Configure(EntityTypeBuilder<ItineraryDay> builder)
    {
        builder.ToTable("ItineraryDays");

        builder.HasKey(d => d.ItineraryDayId);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Description)
            .IsRequired()
            .HasColumnType("text");

        builder.HasIndex(d => new { d.TripId, d.DisplayOrder });

        builder.HasMany(d => d.ItineraryPoints)
            .WithOne(p => p.ItineraryDay)
            .HasForeignKey(p => p.ItineraryDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
