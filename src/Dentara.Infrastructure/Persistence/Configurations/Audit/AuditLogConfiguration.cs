namespace Dentara.Infrastructure.Persistence.Configurations.Audit;

using Dentara.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.PerformedBy).HasMaxLength(200).IsRequired();
        builder.Property(a => a.PerformedByUserId);
        builder.Property(a => a.OldValueJson).HasMaxLength(int.MaxValue);
        builder.Property(a => a.NewValueJson).HasMaxLength(int.MaxValue);
        builder.Property(a => a.Description).HasMaxLength(1000);
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.Property(a => a.CorrelationId).HasMaxLength(100);
        builder.Property(a => a.OccurredAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        // NO RowVersion — AuditLog is INSERT-only, never updated, so no
        // optimistic concurrency is needed.

        // Indexes for common audit queries
        builder.HasIndex(a => new { a.EntityType, a.EntityId })
            .HasDatabaseName("IX_AuditLogs_EntityType_EntityId");
        builder.HasIndex(a => a.OccurredAtUtc).HasDatabaseName("IX_AuditLogs_OccurredAtUtc");
        builder.HasIndex(a => a.PerformedByUserId).HasDatabaseName("IX_AuditLogs_PerformedByUserId");
        builder.HasIndex(a => a.CorrelationId).HasDatabaseName("IX_AuditLogs_CorrelationId");
    }
}
