namespace Dentara.Infrastructure.Persistence.Configurations.Notifications;

using Dentara.Domain.Notifications;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.AppointmentId).IsRequired();
        builder.Property(n => n.PatientId).IsRequired();
        builder.Property(n => n.TypeValue).IsRequired();
        builder.Property(n => n.ChannelValue).IsRequired();
        builder.Property(n => n.StatusValue).IsRequired();
        builder.Property(n => n.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(n => n.MessageBody).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.ScheduledAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(n => n.SentAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(n => n.FailedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(n => n.AttemptCount).IsRequired();
        builder.Property(n => n.FailureReason).HasMaxLength(500);
        builder.Property(n => n.CorrelationId).HasMaxLength(100);
        builder.Property(n => n.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(n => n.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(n => n.RowVersion).ConfigureRowVersion();

        // Index for the dispatcher: find pending notifications that are due
        builder.HasIndex(n => new { n.StatusValue, n.ScheduledAtUtc })
            .HasDatabaseName("IX_Notifications_Status_ScheduledAtUtc");

        // Index for finding reminders for a specific appointment (prevent duplicates)
        builder.HasIndex(n => new { n.AppointmentId, n.TypeValue })
            .HasDatabaseName("IX_Notifications_AppointmentId_TypeValue");

        builder.HasIndex(n => n.PatientId).HasDatabaseName("IX_Notifications_PatientId");
    }
}
