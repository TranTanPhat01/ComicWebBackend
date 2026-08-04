using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ComicWeb.Persistence.Configurations;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{ public void Configure(EntityTypeBuilder<RefreshSession> b) { b.HasKey(x => x.Id); b.Property(x => x.TokenHash).IsRequired().HasMaxLength(64); b.Property(x => x.JwtId).IsRequired().HasMaxLength(64); b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.ExpiresAt }); b.HasIndex(x => new { x.UserId, x.RevokedAt }); b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); } }
