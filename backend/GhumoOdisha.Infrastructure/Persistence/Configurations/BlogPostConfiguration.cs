using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("BlogPosts");

        builder.HasKey(p => p.BlogPostId);

        builder.Property(p => p.Title).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Place).HasMaxLength(100);
        builder.Property(p => p.Excerpt).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Content).IsRequired().HasColumnType("mediumtext");
        builder.Property(p => p.HeroImageUrl).HasMaxLength(500);
        builder.Property(p => p.Tags).HasColumnType("text");

        builder.Property(p => p.PublishedAt).HasColumnType("datetime(6)");
        builder.Property(p => p.CreatedAt).HasColumnType("datetime(6)");
        builder.Property(p => p.UpdatedAt).HasColumnType("datetime(6)");

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => new { p.IsPublished, p.PublishedAt });

        builder.HasMany(p => p.Photos)
            .WithOne(ph => ph.BlogPost)
            .HasForeignKey(ph => ph.BlogPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
