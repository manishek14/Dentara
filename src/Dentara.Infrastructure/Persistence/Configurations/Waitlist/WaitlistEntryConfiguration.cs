namespace Dentara.Infrastructure.Persistence.Configurations.Waitlist;

using Dentara.Domain.Waitlist;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("WaitlistEntries");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.PatientId).IsRequired();
        builder.Property(w => w.DentistId).IsRequired();
        builder.Property(w => w.ServiceId).IsRequired();
        builder.Property(w => w.EarliestStartUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(w => w.LatestStartUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(w => w.StatusValue).IsRequired();
        builder.Property(w => w.Priority).IsRequired();
        builder.Property(w => w.OfferedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.OfferExpiresAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.OfferedStartUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.FulfilledAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.DeclinedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.CancelledAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.DeclineReason).HasMaxLength(500);
        builder.Property(w => w.Notes).HasMaxLength(1000);
        builder.Property(w => w.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(w => w.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.RowVersion).ConfigureRowVersion();

        // Index for finding waiting entries for a dentist+service (ordered by priority)
        builder.HasIndex(w => new { w.DentistId, w.ServiceId, w.StatusValue, w.Priority })
            .HasDatabaseName("IX_WaitlistEntries_Dentist_Service_Status_Priority");

        // Index for finding expired offers
        builder.HasIndex(w => new { w.StatusValue, w.OfferExpiresAtUtc })
            .HasDatabaseName("IX_WaitlistEntries_Status_OfferExpiresAtUtc");

        builder.HasIndex(w => w.PatientId).HasDatabaseName("IX_WaitlistEntries_PatientId");
    }
}
