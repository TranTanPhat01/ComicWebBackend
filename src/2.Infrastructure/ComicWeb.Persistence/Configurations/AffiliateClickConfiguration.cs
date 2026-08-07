using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class AffiliateClickConfiguration : IEntityTypeConfiguration<AffiliateClick>
{
    public void Configure(EntityTypeBuilder<AffiliateClick> b)
    {
        b.HasKey(x => x.Id);
        
        b.Property(x => x.IpAddress).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(500);
        b.Property(x => x.Referrer).HasMaxLength(500);

        b.HasOne(x => x.Chapter)
            .WithMany()
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Story)
            .WithMany()
            .HasForeignKey(x => x.StoryId)
            .OnDelete(DeleteBehavior.Restrict);
            
        b.HasIndex(x => x.ChapterId);
        b.HasIndex(x => x.StoryId);
        b.HasIndex(x => x.ClickedAt);
    }
}
