using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicWeb.Persistence.Configurations
{
    public class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
    {
        public void Configure(EntityTypeBuilder<Chapter> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Title)
                .HasMaxLength(250);

            builder.Property(c => c.Content)
                .IsRequired(); // Nội dung chữ của truyện bắt buộc phải có

            builder.Property(c => c.AffiliateLink)
                .HasMaxLength(500); // Giới hạn link Shopee tránh quá dài

            // Cấu hình quan hệ 1-Nhiều (1 Truyện có Nhiều Chương)
            builder.HasOne(c => c.Story)
                .WithMany(s => s.Chapters)
                .HasForeignKey(c => c.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
