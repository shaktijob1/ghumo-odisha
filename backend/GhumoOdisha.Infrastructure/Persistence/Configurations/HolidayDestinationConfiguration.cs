using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class HolidayDestinationConfiguration : IEntityTypeConfiguration<HolidayDestination>
{
    public void Configure(EntityTypeBuilder<HolidayDestination> builder)
    {
        builder.ToTable("HolidayDestinations");
        builder.HasKey(d => d.HolidayDestinationId);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Tagline).HasMaxLength(150);
        builder.Property(d => d.CoverImageUrl).HasMaxLength(500);
        builder.Property(d => d.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(d => d.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(d => d.Name).IsUnique();
        builder.HasIndex(d => new { d.IsActive, d.DisplayOrder });

        // Deleting a destination keeps its packages, just unlinked.
        builder.HasMany(d => d.Packages)
            .WithOne(p => p.HolidayDestination)
            .HasForeignKey(p => p.HolidayDestinationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
