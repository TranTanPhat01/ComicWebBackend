using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.Property(x => x.Slug).IsRequired().HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.IsActive).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.IsActive);

        b.HasMany(x => x.Stories)
            .WithMany(x => x.Genres)
            .UsingEntity(j => j.ToTable("StoryGenres"));
    }
}
