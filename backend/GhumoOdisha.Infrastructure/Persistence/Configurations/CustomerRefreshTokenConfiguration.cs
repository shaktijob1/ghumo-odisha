using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CustomerRefreshTokenConfiguration : IEntityTypeConfiguration<CustomerRefreshToken>
{
    public void Configure(EntityTypeBuilder<CustomerRefreshToken> builder)
    {
        builder.ToTable("CustomerRefreshTokens");

        builder.HasKey(r => r.CustomerRefreshTokenId);

        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.ExpiresAt).HasColumnType("datetime(6)");
        builder.Property(r => r.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(r => r.RevokedAt).HasColumnType("datetime(6)");

        builder.HasIndex(r => r.TokenHash).IsUnique();
        builder.HasIndex(r => r.CustomerId);
    }
}
