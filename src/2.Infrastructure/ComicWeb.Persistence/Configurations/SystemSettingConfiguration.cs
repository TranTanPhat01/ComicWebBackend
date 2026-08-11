using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.HasKey(x => x.Id);
        
        b.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(100);
            
        b.Property(x => x.Value)
            .IsRequired();
            
        b.Property(x => x.Description)
            .HasMaxLength(250);

        b.HasIndex(x => x.Key)
            .IsUnique();
    }
}
