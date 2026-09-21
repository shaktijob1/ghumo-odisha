using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("AdminUsers");

        builder.HasKey(a => a.AdminUserId);

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(a => a.LastLoginAt).HasColumnType("datetime(6)");

        builder.HasIndex(a => a.Username).IsUnique();
    }
}
