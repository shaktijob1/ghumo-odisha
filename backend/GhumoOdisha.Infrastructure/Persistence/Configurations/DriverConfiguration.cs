using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers", t =>
        {
            t.HasCheckConstraint("CK_Driver_ExperienceYears", "ExperienceYears IS NULL OR (ExperienceYears >= 0 AND ExperienceYears <= 60)");
        });

        builder.HasKey(d => d.DriverId);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(150);
        builder.Property(d => d.PhoneNumber).HasMaxLength(15);
        builder.Property(d => d.Email).HasMaxLength(200);
        builder.Property(d => d.GoogleSubject).HasMaxLength(64);
        builder.Property(d => d.Address).HasMaxLength(500);
        builder.Property(d => d.City).HasMaxLength(100);
        builder.Property(d => d.DrivingLicenceNumber).HasMaxLength(30);
        builder.Property(d => d.ProfilePhotoUrl).HasMaxLength(500);
        builder.Property(d => d.StatusReason).HasMaxLength(500);
        // Concurrency token: an admin decision and a driver edit racing on the same row can't silently overwrite each other.
        builder.Property(d => d.Status).HasConversion<int>().IsConcurrencyToken();

        builder.Property(d => d.SubmittedForReviewAt).HasColumnType("datetime(6)");
        builder.Property(d => d.ApprovedAt).HasColumnType("datetime(6)");
        builder.Property(d => d.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(d => d.UpdatedAt).HasColumnType("datetime(6)");
        builder.Property(d => d.LastLoginAt).HasColumnType("datetime(6)");

        // MySQL unique indexes allow many NULLs, so Google-only drivers without a phone coexist.
        builder.HasIndex(d => d.PhoneNumber).IsUnique();
        builder.HasIndex(d => d.GoogleSubject).IsUnique();
        builder.HasIndex(d => d.Email);
        builder.HasIndex(d => d.Status);

        builder.HasMany(d => d.RefreshTokens)
            .WithOne(r => r.Driver)
            .HasForeignKey(r => r.DriverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
