using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ComicWeb.Persistence.Configurations;

public sealed class StoryConfiguration : IEntityTypeConfiguration<Story>
{ public void Configure(EntityTypeBuilder<Story> b) { b.HasKey(x => x.Id); b.Property(x => x.Title).IsRequired().HasMaxLength(250); b.Property(x => x.Slug).IsRequired().HasMaxLength(250); b.Property(x => x.Description).HasMaxLength(4000); b.Property(x => x.AuthorName).HasMaxLength(250); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken(); b.HasQueryFilter(x => x.DeletedAt == null); b.HasIndex(x => x.Slug).IsUnique().HasFilter("\"DeletedAt\" IS NULL"); b.HasIndex(x => new { x.Status, x.PublishedAt }); b.HasIndex(x => new { x.ScheduledAt, x.Status }); } }
