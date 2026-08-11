using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class NewsletterSubscriberConfiguration : IEntityTypeConfiguration<NewsletterSubscriber>
{
    public void Configure(EntityTypeBuilder<NewsletterSubscriber> b)
    {
        b.HasKey(x => x.Id);

        b.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(255);

        b.Property(x => x.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(255);

        b.HasIndex(x => x.NormalizedEmail)
            .IsUnique();
    }
}
