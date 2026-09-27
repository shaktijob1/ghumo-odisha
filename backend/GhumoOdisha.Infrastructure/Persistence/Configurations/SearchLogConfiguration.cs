using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class SearchLogConfiguration : IEntityTypeConfiguration<SearchLog>
{
    public void Configure(EntityTypeBuilder<SearchLog> builder)
    {
        builder.ToTable("SearchLogs");
        builder.HasKey(s => s.SearchLogId);

        builder.Property(s => s.Month).HasMaxLength(7);
        builder.Property(s => s.Place).HasMaxLength(100);
        builder.Property(s => s.ClientIp).HasMaxLength(45);
        builder.Property(s => s.CreatedAtUtc).HasColumnType("datetime(6)");

        builder.HasIndex(s => s.CreatedAtUtc);
        builder.HasIndex(s => new { s.Place, s.CreatedAtUtc });
        builder.HasIndex(s => new { s.Month, s.CreatedAtUtc });
    }
}
