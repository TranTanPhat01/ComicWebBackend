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
            builder.Property(u => u.NormalizedUsername).IsRequired().HasMaxLength(100);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
            builder.Property(u => u.NormalizedEmail).IsRequired().HasMaxLength(320);
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.Role).HasConversion<string>().IsRequired();
            builder.Property(u => u.IsActive).HasDefaultValue(true);
            builder.Property(u => u.MustChangePassword).HasDefaultValue(false);
            builder.HasIndex(u => u.NormalizedUsername).IsUnique();
            builder.HasIndex(u => u.NormalizedEmail).IsUnique();
        }
    }
}
