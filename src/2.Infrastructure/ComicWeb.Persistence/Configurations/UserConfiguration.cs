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
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
            builder.HasIndex(u => u.Username).IsUnique();

            // Sử dụng thư viện BCrypt.Net-Next (hoặc tương đương) để hash mật khẩu trước khi lưu DB
            // Để đơn giản hóa ở bước này, ta giả định mật khẩu đã được hash
            string staticHash = BCrypt.Net.BCrypt.HashPassword("Admin@system");

            // Tự động chèn dữ liệu khi database được khởi tạo hoặc cập nhật
            builder.HasData(new User
            {
                Id = 1,
                Username = "Admin",
                PasswordHash = staticHash,
                Email = "Admin@gmail.com",
                Role = "Admin",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        }
    }
}
