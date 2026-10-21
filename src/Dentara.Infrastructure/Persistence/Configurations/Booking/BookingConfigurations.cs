namespace Dentara.Infrastructure.Persistence.Configurations.Booking;

using Dentara.Domain.Booking;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.PatientId).IsRequired();
        builder.Property(a => a.DentistId).IsRequired();
        builder.Property(a => a.ServiceId).IsRequired();
        builder.Property(a => a.PublishedSessionId);
        builder.Property(a => a.StartAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(a => a.EndAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(a => a.StatusValue).IsRequired();
        builder.Property(a => a.ExpiresAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();

        // Snapshot fields (ADR-007)
        builder.Property(a => a.ServiceNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ServiceDurationMinutesSnapshot).IsRequired();
        builder.Property(a => a.PriceAmountSnapshot).HasColumnType("decimal(18, 2)").IsRequired();
        builder.Property(a => a.CurrencySnapshot).HasMaxLength(3).IsRequired();
        builder.Property(a => a.DentistDisplayNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(a => a.CancellationPolicyVersionSnapshot).IsRequired();
        builder.Property(a => a.CancellationFeePercentSnapshot).HasColumnType("decimal(5, 2)").IsRequired();
        builder.Property(a => a.RefundablePercentSnapshot).HasColumnType("decimal(5, 2)").IsRequired();
        builder.Property(a => a.TermsSnapshotHash).HasMaxLength(128);

        // Lifecycle timestamps
        builder.Property(a => a.ConfirmedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(a => a.ExpiredAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(a => a.CancelledAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(a => a.CompletedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(a => a.NoShowAtUtc).HasColumnType("datetimeoffset(0)");

        builder.Property(a => a.CancelledBy).HasMaxLength(100);
        builder.Property(a => a.CancellationReason).HasMaxLength(500);

        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(a => a.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(a => a.RowVersion).ConfigureRowVersion();

        // CHECK constraints per architecture spec
        builder.ToTable(a => a.HasCheckConstraint(
            "CK_Appointments_StartBeforeEnd",
            "StartAtUtc < EndAtUtc"));
        builder.ToTable(a => a.HasCheckConstraint(
            "CK_Appointments_StatusValue",
            "StatusValue IN (1, 2, 3, 4, 5, 6)"));
        builder.ToTable(a => a.HasCheckConstraint(
            "CK_Appointments_FeePlusRefundableEqual100",
            "ABS(CancellationFeePercentSnapshot + RefundablePercentSnapshot - 100.0) < 0.01"));

        // Indexes per architecture spec
        builder.HasIndex(a => new { a.PatientId, a.CreatedAtUtc })
            .HasDatabaseName("IX_Appointments_PatientId_CreatedAtUtc");
        builder.HasIndex(a => new { a.DentistId, a.StartAtUtc, a.EndAtUtc })
            .HasDatabaseName("IX_Appointments_DentistId_StartAtUtc_EndAtUtc");
        // Filtered index for finding expired PendingPayment appointments
        builder.HasIndex(a => new { a.StatusValue, a.ExpiresAtUtc })
            .HasFilter("StatusValue = 1") // PendingPayment = 1
            .HasDatabaseName("IX_Appointments_PendingPayment_ExpiresAtUtc");

        // Navigation: ResourceAllocations are loaded via Include in services.
        // Events are added explicitly via DbContext.AppointmentEvents.Add.
        // We don't configure HasMany/WithOne here because the navigation
        // collections are IReadOnlyCollection (not ICollection), which EF Core
        // manages via backing field auto-discovery.
    }
}

public sealed class ResourceAllocationConfiguration : IEntityTypeConfiguration<ResourceAllocation>
{
    public void Configure(EntityTypeBuilder<ResourceAllocation> builder)
    {
        builder.ToTable("ResourceAllocations");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.AppointmentId).IsRequired();
        builder.Property(r => r.ResourceId).IsRequired();
        builder.Property(r => r.ResourceTypeSnapshotValue).IsRequired();
        builder.Property(r => r.StartAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.EndAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.StatusValue).IsRequired();
        builder.Property(r => r.ReleasedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.ReleaseReason).HasMaxLength(500);
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.RowVersion).ConfigureRowVersion();

        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ResourceAllocations_StartBeforeEnd",
            "StartAtUtc < EndAtUtc"));
        builder.ToTable(r => r.HasCheckConstraint(
            "CK_ResourceAllocations_StatusValue",
            "StatusValue IN (1, 2)")); // Active=1, Released=2

        // Critical index for ADR-003 overlap check
        builder.HasIndex(r => new { r.ResourceId, r.StartAtUtc, r.EndAtUtc })
            .HasDatabaseName("IX_ResourceAllocations_ResourceId_StartAtUtc_EndAtUtc");
        builder.HasIndex(r => r.AppointmentId).HasDatabaseName("IX_ResourceAllocations_AppointmentId");
        // Filtered index for active allocations only (used by overlap check)
        builder.HasIndex(r => new { r.ResourceId, r.StartAtUtc, r.EndAtUtc })
            .HasFilter("StatusValue = 1") // Active=1
            .HasDatabaseName("IX_ResourceAllocations_Active_Overlap");
    }
}

public sealed class AppointmentEventConfiguration : IEntityTypeConfiguration<AppointmentEvent>
{
    public void Configure(EntityTypeBuilder<AppointmentEvent> builder)
    {
        builder.ToTable("AppointmentEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.AppointmentId).IsRequired();
        builder.Property(e => e.EventTypeValue).IsRequired();
        builder.Property(e => e.OccurredAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.PayloadJson).HasMaxLength(int.MaxValue);
        builder.Property(e => e.CorrelationId).HasMaxLength(100);
        builder.Property(e => e.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(e => e.RowVersion).ConfigureRowVersion();

        builder.ToTable(e => e.HasCheckConstraint(
            "CK_AppointmentEvents_EventTypeValue",
            "EventTypeValue IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)"));

        builder.HasIndex(e => e.AppointmentId).HasDatabaseName("IX_AppointmentEvents_AppointmentId");
        builder.HasIndex(e => new { e.AppointmentId, e.OccurredAtUtc })
            .HasDatabaseName("IX_AppointmentEvents_AppointmentId_OccurredAtUtc");
        builder.HasIndex(e => e.CorrelationId).HasDatabaseName("IX_AppointmentEvents_CorrelationId");
    }
}
