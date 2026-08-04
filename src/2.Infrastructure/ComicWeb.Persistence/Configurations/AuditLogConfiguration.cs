using ComicWeb.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComicWeb.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ActorUsername)
            .HasMaxLength(100);

        builder.Property(x => x.ActorType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasMaxLength(100);

        builder.Property(x => x.Result)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.RequestId)
            .HasMaxLength(200);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(64);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(1000);

        builder.Property(x => x.ErrorCode)
            .HasMaxLength(100);

        // Store DetailsJson as jsonb in PostgreSQL
        builder.Property(x => x.DetailsJson)
            .HasColumnType("jsonb");

        // OccurredAt UTC
        builder.Property(x => x.OccurredAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.Action, x.OccurredAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
        builder.HasIndex(x => new { x.ActorUserId, x.OccurredAt });
        builder.HasIndex(x => new { x.Result, x.OccurredAt });
    }
}
