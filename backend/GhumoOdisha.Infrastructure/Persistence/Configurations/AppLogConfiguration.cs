using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class AppLogConfiguration : IEntityTypeConfiguration<AppLog>
{
    public void Configure(EntityTypeBuilder<AppLog> builder)
    {
        builder.ToTable("AppLogs");
        builder.HasKey(l => l.AppLogId);

        builder.Property(l => l.TimestampUtc).HasColumnType("datetime(6)");
        builder.Property(l => l.Level).HasMaxLength(12).IsRequired();
        builder.Property(l => l.Message).HasColumnType("text").IsRequired();
        builder.Property(l => l.Exception).HasColumnType("mediumtext");
        builder.Property(l => l.Source).HasMaxLength(200);
        builder.Property(l => l.RequestId).HasMaxLength(16);
        builder.Property(l => l.RequestMethod).HasMaxLength(10);
        builder.Property(l => l.RequestPath).HasMaxLength(300);
        builder.Property(l => l.UserRole).HasMaxLength(20);
        builder.Property(l => l.ClientIp).HasMaxLength(45);
        builder.Property(l => l.PropertiesJson).HasColumnType("text");

        builder.HasIndex(l => l.TimestampUtc);
        builder.HasIndex(l => new { l.Level, l.TimestampUtc });
        builder.HasIndex(l => l.RequestId);
        builder.HasIndex(l => new { l.UserRole, l.UserId, l.TimestampUtc });
    }
}

public class AdminActivityConfiguration : IEntityTypeConfiguration<AdminActivity>
{
    public void Configure(EntityTypeBuilder<AdminActivity> builder)
    {
        builder.ToTable("AdminActivities");
        builder.HasKey(a => a.AdminActivityId);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Area).HasMaxLength(60).IsRequired();
        builder.Property(a => a.HttpMethod).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Path).HasMaxLength(300).IsRequired();
        builder.Property(a => a.RequestId).HasMaxLength(16);
        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetime(6)");

        builder.HasIndex(a => a.CreatedAtUtc);
        builder.HasIndex(a => new { a.Area, a.TargetId });
    }
}
