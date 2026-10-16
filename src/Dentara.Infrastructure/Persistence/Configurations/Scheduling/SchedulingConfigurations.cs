namespace Dentara.Infrastructure.Persistence.Configurations.Scheduling;

using Dentara.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        builder.ToTable("WorkSchedules");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.DentistId).IsRequired();
        builder.Property(w => w.DayOfWeekValue).IsRequired();
        builder.Property(w => w.StartTime).HasColumnType("time").IsRequired();
        builder.Property(w => w.EndTime).HasColumnType("time").IsRequired();
        builder.Property(w => w.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(w => w.EffectiveFromUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.EffectiveUntilUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(w => w.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.RowVersion).IsRowVersion();

        // CHECK constraints per architecture spec
        builder.ToTable(w => w.HasCheckConstraint(
            "CK_WorkSchedules_DayOfWeekRange",
            "DayOfWeekValue >= 0 AND DayOfWeekValue <= 6"));
        builder.ToTable(w => w.HasCheckConstraint(
            "CK_WorkSchedules_StartBeforeEnd",
            "StartTime < EndTime"));

        builder.HasIndex(w => w.DentistId).HasDatabaseName("IX_WorkSchedules_DentistId");
        builder.HasIndex(w => new { w.DentistId, w.DayOfWeekValue })
            .HasDatabaseName("IX_WorkSchedules_DentistId_DayOfWeek");
    }
}

public sealed class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public void Configure(EntityTypeBuilder<ScheduleException> builder)
    {
        builder.ToTable("ScheduleExceptions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.DentistId).IsRequired(); // Guid.Empty = clinic-wide; CHECK constraints enforce semantics
        // Note: DentistId is non-nullable Guid in domain; Guid.Empty represents clinic-wide.
        // This is intentional — a nullable Guid? would complicate the domain model.
        builder.Property(e => e.TypeValue).IsRequired();
        builder.Property(e => e.EffectiveFromUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(e => e.EffectiveUntilUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(e => e.WindowStartTime).HasColumnType("time");
        builder.Property(e => e.WindowEndTime).HasColumnType("time");
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(e => e.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.ToTable(e => e.HasCheckConstraint(
            "CK_ScheduleExceptions_TypeValue",
            "TypeValue IN (1, 2, 3, 4)"));
        builder.ToTable(e => e.HasCheckConstraint(
            "CK_ScheduleExceptions_FromBeforeUntil",
            "EffectiveFromUtc < EffectiveUntilUtc"));
        // ReducedHours and AdditionalHours must have window times; Unavailability and Holiday must not.
        builder.ToTable(e => e.HasCheckConstraint(
            "CK_ScheduleExceptions_WindowConsistency",
            "((TypeValue IN (3, 4) AND WindowStartTime IS NOT NULL AND WindowEndTime IS NOT NULL) OR " +
            " (TypeValue IN (1, 2) AND WindowStartTime IS NULL AND WindowEndTime IS NULL))"));

        builder.HasIndex(e => e.DentistId).HasDatabaseName("IX_ScheduleExceptions_DentistId");
        builder.HasIndex(e => e.EffectiveFromUtc).HasDatabaseName("IX_ScheduleExceptions_EffectiveFromUtc");
    }
}

public sealed class BookingWindowConfiguration : IEntityTypeConfiguration<BookingWindow>
{
    public void Configure(EntityTypeBuilder<BookingWindow> builder)
    {
        builder.ToTable("BookingWindows");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.OpensAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(b => b.ClosesAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(b => b.ApplicableDateFromUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(b => b.ApplicableDateUntilUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(b => b.StatusValue).IsRequired();
        builder.Property(b => b.DentistIdFilter);
        builder.Property(b => b.Description).HasMaxLength(500);
        builder.Property(b => b.OpenedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(b => b.ClosedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(b => b.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(b => b.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(b => b.RowVersion).IsRowVersion();

        builder.ToTable(b => b.HasCheckConstraint(
            "CK_BookingWindows_StatusValue",
            "StatusValue IN (1, 2, 3, 4)"));
        builder.ToTable(b => b.HasCheckConstraint(
            "CK_BookingWindows_OpensBeforeCloses",
            "OpensAtUtc < ClosesAtUtc"));
        builder.ToTable(b => b.HasCheckConstraint(
            "CK_BookingWindows_ApplicableRangeValid",
            "ApplicableDateFromUtc < ApplicableDateUntilUtc"));

        // Index for the publication worker: WHERE Status=1 AND OpensAtUtc <= NOW
        builder.HasIndex(b => new { b.StatusValue, b.OpensAtUtc })
            .HasDatabaseName("IX_BookingWindows_Status_OpensAtUtc");
    }
}

public sealed class PublishedSessionConfiguration : IEntityTypeConfiguration<PublishedSession>
{
    public void Configure(EntityTypeBuilder<PublishedSession> builder)
    {
        builder.ToTable("PublishedSessions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.BookingWindowId).IsRequired();
        builder.Property(p => p.DentistId).IsRequired();
        builder.Property(p => p.StartAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(p => p.EndAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(p => p.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(p => p.HasEverBeenBooked).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.PublishedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(p => p.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(p => p.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.RowVersion).IsRowVersion();

        // UNIQUE on IdempotencyKey — prevents duplicate session generation per ADR-006
        builder.HasIndex(p => p.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_PublishedSessions_IdempotencyKey");

        builder.HasIndex(p => new { p.DentistId, p.StartAtUtc })
            .HasDatabaseName("IX_PublishedSessions_DentistId_StartAtUtc");
        builder.HasIndex(p => p.BookingWindowId).HasDatabaseName("IX_PublishedSessions_BookingWindowId");
    }
}
