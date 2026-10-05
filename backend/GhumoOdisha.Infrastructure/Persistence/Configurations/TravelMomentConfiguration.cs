using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class TravelMomentConfiguration : IEntityTypeConfiguration<TravelMoment>
{
    public void Configure(EntityTypeBuilder<TravelMoment> builder)
    {
        builder.ToTable("TravelMoments");

        builder.HasKey(m => m.TravelMomentId);

        builder.Property(m => m.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(m => m.Caption).HasMaxLength(150);
        builder.Property(m => m.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(m => m.DisplayOrder);
    }
}
