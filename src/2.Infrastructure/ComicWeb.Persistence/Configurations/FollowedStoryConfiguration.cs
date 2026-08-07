using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class FollowedStoryConfiguration : IEntityTypeConfiguration<FollowedStory>
{
    public void Configure(EntityTypeBuilder<FollowedStory> b)
    {
        b.HasKey(x => x.Id);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Story)
            .WithMany()
            .HasForeignKey(x => x.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.StoryId })
            .IsUnique();
            
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.StoryId);
    }
}
