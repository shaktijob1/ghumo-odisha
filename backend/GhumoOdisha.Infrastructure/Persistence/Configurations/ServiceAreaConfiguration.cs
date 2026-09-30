using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("ServiceAreas");
        builder.HasKey(a => a.ServiceAreaId);

        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
        builder.Property(a => a.BoundaryJson).HasColumnType("text");
        builder.Property(a => a.Pincodes).HasMaxLength(2000);
        builder.Property(a => a.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(a => a.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(a => a.IsActive);
    }
}
