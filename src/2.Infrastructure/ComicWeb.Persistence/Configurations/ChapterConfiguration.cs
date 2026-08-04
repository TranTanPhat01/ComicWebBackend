using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ComicWeb.Persistence.Configurations;

public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{ public void Configure(EntityTypeBuilder<Chapter> b) { b.HasKey(x => x.Id); b.Property(x => x.Title).IsRequired().HasMaxLength(250); b.Property(x => x.Content).IsRequired(); b.Property(x => x.Slug).IsRequired().HasMaxLength(250); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired(); b.Property(x => x.Version).IsConcurrencyToken(); b.HasQueryFilter(x => x.DeletedAt == null); b.HasOne(x => x.Story).WithMany(x => x.Chapters).HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(x => new { x.StoryId, x.ChapterNumber }).IsUnique().HasFilter("\"DeletedAt\" IS NULL"); b.HasIndex(x => new { x.StoryId, x.Slug }).IsUnique().HasFilter("\"DeletedAt\" IS NULL"); b.HasIndex(x => new { x.StoryId, x.Status, x.ChapterNumber }); b.HasIndex(x => new { x.ScheduledAt, x.Status }); } }
