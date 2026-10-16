namespace Dentara.Infrastructure.Persistence.Configurations.Payments;

using Dentara.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.AppointmentId).IsRequired();
        builder.Property(p => p.PatientId).IsRequired();
        builder.Property(p => p.ProviderValue).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18, 2)").IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.StatusValue).IsRequired();
        builder.Property(p => p.GatewayAuthority).HasMaxLength(200);
        builder.Property(p => p.ProviderTransactionId).HasMaxLength(200);
        builder.Property(p => p.GatewayRedirectUrl).HasMaxLength(1000);
        builder.Property(p => p.InitiatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.SucceededAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.FailedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.LastCallbackAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.FailureReason).HasMaxLength(1000);
        builder.Property(p => p.TermsSnapshotHash).HasMaxLength(128);
        builder.Property(p => p.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(p => p.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.ToTable(p => p.HasCheckConstraint(
            "CK_PaymentAttempts_AmountPositive",
            "Amount > 0"));
        builder.ToTable(p => p.HasCheckConstraint(
            "CK_PaymentAttempts_ProviderValue",
            "ProviderValue IN (1, 2, 3, 4)"));
        builder.ToTable(p => p.HasCheckConstraint(
            "CK_PaymentAttempts_StatusValue",
            "StatusValue IN (1, 2, 3, 4, 5, 6, 7, 8, 9)"));
        builder.ToTable(p => p.HasCheckConstraint(
            "CK_PaymentAttempts_CurrencyLength",
            "LEN(Currency) = 3"));

        // UNIQUE on (Provider, ProviderTransactionId) WHERE ProviderTransactionId IS NOT NULL
        // Per architecture spec: "شناسه تراکنش درگاه، در صورت وجود، با قید یکتا و متناسب با ارائه‌دهنده"
        builder.HasIndex(p => new { p.ProviderValue, p.ProviderTransactionId })
            .IsUnique()
            .HasFilter("ProviderTransactionId IS NOT NULL")
            .HasDatabaseName("UX_PaymentAttempts_Provider_TransactionId");

        builder.HasIndex(p => p.AppointmentId).HasDatabaseName("IX_PaymentAttempts_AppointmentId");
        builder.HasIndex(p => new { p.StatusValue, p.LastCallbackAtUtc })
            .HasDatabaseName("IX_PaymentAttempts_Status_LastCallbackAtUtc");
    }
}

public sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PaymentAttemptId).IsRequired();
        builder.Property(r => r.AppointmentId).IsRequired();
        builder.Property(r => r.Amount).HasColumnType("decimal(18, 2)").IsRequired();
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.StatusValue).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.ProviderRefundId).HasMaxLength(200);
        builder.Property(r => r.RequestedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.SucceededAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.FailedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.FailureReason).HasMaxLength(1000);
        builder.Property(r => r.CorrelationId).HasMaxLength(100);
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.ToTable(r => r.HasCheckConstraint(
            "CK_Refunds_AmountPositive",
            "Amount > 0"));
        builder.ToTable(r => r.HasCheckConstraint(
            "CK_Refunds_StatusValue",
            "StatusValue IN (1, 2, 3)"));

        builder.HasIndex(r => r.PaymentAttemptId).HasDatabaseName("IX_Refunds_PaymentAttemptId");
        builder.HasIndex(r => r.AppointmentId).HasDatabaseName("IX_Refunds_AppointmentId");
    }
}

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.PaymentAttemptId).IsRequired();
        builder.Property(w => w.ProviderValue).IsRequired();
        builder.Property(w => w.ProviderEventId).HasMaxLength(200).IsRequired();
        builder.Property(w => w.EventType).HasMaxLength(100).IsRequired();
        builder.Property(w => w.ReceivedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(w => w.RawPayload).HasMaxLength(int.MaxValue).IsRequired();
        builder.Property(w => w.SignatureHeader).HasMaxLength(2000);
        builder.Property(w => w.SignatureVerified).IsRequired();
        builder.Property(w => w.ProcessedSuccessfully).IsRequired();
        builder.Property(w => w.ProcessingNotes).HasMaxLength(1000);
        builder.Property(w => w.CorrelationId).HasMaxLength(100);
        builder.Property(w => w.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(w => w.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(w => w.RowVersion).IsRowVersion();

        builder.ToTable(w => w.HasCheckConstraint(
            "CK_PaymentWebhookEvents_ProviderValue",
            "ProviderValue IN (1, 2, 3, 4)"));

        // UNIQUE(Provider, ProviderEventId) per architecture spec — idempotency
        builder.HasIndex(w => new { w.ProviderValue, w.ProviderEventId })
            .IsUnique()
            .HasDatabaseName("UX_PaymentWebhookEvents_Provider_EventId");
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Scope).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Key).HasMaxLength(256).IsRequired();
        builder.Property(r => r.UserId);
        builder.Property(r => r.RequestHash).HasMaxLength(128);
        builder.Property(r => r.StatusValue).IsRequired();
        builder.Property(r => r.ResponsePayload).HasMaxLength(int.MaxValue);
        builder.Property(r => r.ResponseStatusCode);
        builder.Property(r => r.CompletedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.ExpiresAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.CorrelationId).HasMaxLength(100);
        builder.Property(r => r.CreatedAtUtc).HasColumnType("datetimeoffset(0)").IsRequired();
        builder.Property(r => r.UpdatedAtUtc).HasColumnType("datetimeoffset(0)");
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.ToTable(r => r.HasCheckConstraint(
            "CK_IdempotencyRecords_StatusValue",
            "StatusValue IN (1, 2, 3)"));

        // UNIQUE(Scope, Key) per architecture spec
        builder.HasIndex(r => new { r.Scope, r.Key })
            .IsUnique()
            .HasDatabaseName("UX_IdempotencyRecords_Scope_Key");

        // Filtered index for finding in-progress records
        builder.HasIndex(r => r.StatusValue)
            .HasFilter("StatusValue = 1") // InProgress=1
            .HasDatabaseName("IX_IdempotencyRecords_InProgress");
    }
}
