using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class CustomerOtpConfiguration : IEntityTypeConfiguration<CustomerOtp>
{
    public void Configure(EntityTypeBuilder<CustomerOtp> builder)
    {
        builder.ToTable("CustomerOtps");

        builder.HasKey(o => o.CustomerOtpId);

        builder.Property(o => o.PhoneNumber)
            .IsRequired()
            .HasMaxLength(15);

        builder.Property(o => o.Name)
            .HasMaxLength(150);

        builder.Property(o => o.OtpHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(o => o.ExpiresAt).HasColumnType("datetime(6)");
        builder.Property(o => o.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(o => o.UsedAt).HasColumnType("datetime(6)");

        builder.HasIndex(o => new { o.PhoneNumber, o.IsUsed });
    }
}
