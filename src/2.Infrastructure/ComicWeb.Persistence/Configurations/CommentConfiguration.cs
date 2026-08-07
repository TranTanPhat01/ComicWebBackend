using ComicWeb.Domain.Entities;
using ComicWeb.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        b.Property(x => x.Status)
            .HasDefaultValue(CommentStatus.Active);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Story)
            .WithMany()
            .HasForeignKey(x => x.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ParentComment)
            .WithMany(x => x.Replies)
            .HasForeignKey(x => x.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for efficient queries
        b.HasIndex(x => x.StoryId);
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.ParentCommentId);
        b.HasIndex(x => new { x.StoryId, x.ChapterId });
    }
}
