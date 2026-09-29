using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class DriverDocumentConfiguration : IEntityTypeConfiguration<DriverDocument>
{
    public void Configure(EntityTypeBuilder<DriverDocument> builder)
    {
        builder.ToTable("DriverDocuments");

        builder.HasKey(d => d.DriverDocumentId);

        builder.Property(d => d.DocumentType).HasConversion<int>();
        builder.Property(d => d.FileUrl).IsRequired().HasMaxLength(500);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(50);
        builder.Property(d => d.CreatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(d => new { d.DriverId, d.CarId });

        builder.HasOne(d => d.Driver).WithMany(x => x.Documents).HasForeignKey(d => d.DriverId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(d => d.Car).WithMany().HasForeignKey(d => d.CarId).OnDelete(DeleteBehavior.Cascade);
    }
}
