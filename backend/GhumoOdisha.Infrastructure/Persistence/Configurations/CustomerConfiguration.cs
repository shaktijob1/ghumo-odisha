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
            .IsRequired()
            .HasMaxLength(15);

        builder.Property(c => c.Email)
            .HasMaxLength(200);

        builder.Property(c => c.PinHash)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.UpdatedAt).HasColumnType("datetime(6)");
        builder.Property(c => c.LockoutUntil).HasColumnType("datetime(6)");
        builder.Property(c => c.LastLoginAt).HasColumnType("datetime(6)");

        builder.HasIndex(c => c.PhoneNumber).IsUnique();
        builder.HasIndex(c => c.Email);

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
