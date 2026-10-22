using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.CustomerId);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(15);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.Property(c => c.GoogleSubject)
            .HasMaxLength(64);

        builder.Property(c => c.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.LockoutUntil).HasColumnType("datetime(6)");
        builder.Property(c => c.LastLoginAt).HasColumnType("datetime(6)");

        // CRM fields. No database defaults (an explicit Instagram = 0 / false would be overwritten);
        // the CrmFoundation migration sets existing rows to Website, New Lead, WhatsApp available.
        builder.Property(c => c.AlternatePhoneNumber).HasMaxLength(15);
        builder.Property(c => c.WhatsAppAvailable);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.LeadSource).HasConversion<int>();
        builder.Property(c => c.CrmStatus).HasConversion<int>();
        builder.Property(c => c.CrmSummary).HasMaxLength(2000);
        builder.Property(c => c.LastContactAt).HasColumnType("datetime(6)");

        builder.HasIndex(c => c.AlternatePhoneNumber);
        builder.HasIndex(c => c.CrmStatus);
        builder.HasIndex(c => c.LeadSource);
        builder.HasIndex(c => c.CreatedAt);

        builder.HasOne(c => c.AssignedAdminUser)
            .WithMany()
            .HasForeignKey(c => c.AssignedAdminUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.PhoneNumber).IsUnique();
        builder.HasIndex(c => c.Email);
        // MySQL unique indexes allow many NULLs, so customers without a phone / Google link coexist.
        builder.HasIndex(c => c.GoogleSubject).IsUnique();

        builder.HasMany(c => c.Bookings)
            .WithOne(b => b.Customer)
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.RefreshTokens)
            .WithOne(r => r.Customer)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
