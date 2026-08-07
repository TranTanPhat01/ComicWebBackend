using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class StoryRatingConfiguration : IEntityTypeConfiguration<StoryRating>
{
    public void Configure(EntityTypeBuilder<StoryRating> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.Score)
            .IsRequired();

        b.HasCheckConstraint("CK_StoryRatings_Score", "\"Score\" >= 1 AND \"Score\" <= 5");

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Story)
            .WithMany()
            .HasForeignKey(x => x.StoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: one rating per user per story
        b.HasIndex(x => new { x.UserId, x.StoryId })
            .IsUnique();

        b.HasIndex(x => x.StoryId);
    }
}
