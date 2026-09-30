using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class PaymentQrCodeConfiguration : IEntityTypeConfiguration<PaymentQrCode>
{
    public void Configure(EntityTypeBuilder<PaymentQrCode> builder)
    {
        builder.ToTable("PaymentQrCodes");

        builder.HasKey(q => q.PaymentQrCodeId);

        builder.Property(q => q.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(q => q.Caption).HasMaxLength(200);
        builder.Property(q => q.UpdatedAt).HasColumnType("datetime(6)");
    }
}
