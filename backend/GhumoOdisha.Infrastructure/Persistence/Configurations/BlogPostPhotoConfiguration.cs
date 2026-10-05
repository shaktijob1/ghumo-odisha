using GhumoOdisha.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GhumoOdisha.Infrastructure.Persistence.Configurations;

public class BlogPostPhotoConfiguration : IEntityTypeConfiguration<BlogPostPhoto>
{
    public void Configure(EntityTypeBuilder<BlogPostPhoto> builder)
    {
        builder.ToTable("BlogPostPhotos");

        builder.HasKey(p => p.BlogPostPhotoId);

        builder.Property(p => p.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Caption).HasMaxLength(150);

        builder.HasIndex(p => new { p.BlogPostId, p.DisplayOrder });
    }
}
