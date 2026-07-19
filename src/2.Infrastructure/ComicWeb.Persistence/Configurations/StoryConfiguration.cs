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
    public class StoryConfiguration : IEntityTypeConfiguration<Story>
    {
        public void Configure(EntityTypeBuilder<Story> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Title)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(s => s.Description)
                .HasMaxLength(2000);

            // --- CẤU HÌNH LƯU ENUM THÀNH STRING Ở ĐÂY ---
            builder.Property(s => s.Status)
                .HasConversion<string>() // Tự động map Enum thành String (Ví dụ: Ongoing, Completed)
                .HasMaxLength(50)        // Giới hạn độ dài chuỗi trong DB để tối ưu hiệu năng
                .IsRequired();

            builder.HasIndex(s => s.Title);
        }
    }
}
