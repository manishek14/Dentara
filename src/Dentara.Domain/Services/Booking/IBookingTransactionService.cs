namespace Dentara.Domain.Services.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;

/// <summary>
/// Domain service for the booking transaction orchestration. The Application
/// layer calls this to perform the atomic "check + allocate + reserve" operation
/// per ADR-003. The implementation lives in Infrastructure (uses EF Core +
/// SQL Server transactions with UPDLOCK+HOLDLOCK).
///
/// CRITICAL: this service must NOT call external services (payment gateway,
/// SMS) inside the transaction. The transaction must be short and only touch
/// the database. External calls happen AFTER commit.
/// </summary>
public interface IBookingTransactionService
{
    /// <summary>
    /// Atomically reserves capacity for a new appointment:
    ///   1. Locks the dentist row and required resource rows (UPDLOCK+HOLDLOCK).
    ///   2. Verifies no active appointment overlaps for the dentist.
    ///   3. Verifies no active allocation overlaps for each required resource.
    ///   4. Inserts the Appointment, all ResourceAllocations, and audit events.
    ///   5. Commits the transaction.
    ///
    /// If any step fails, the entire transaction rolls back. The caller receives
    /// either a confirmed-created Appointment or a domain exception (SlotUnavailable,
    /// ResourceUnavailable, DentistNotAvailable).
    /// </summary>
    Task<Appointment> ReserveAppointmentAsync(
        BookingRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable request to reserve a new appointment. Carries all data needed by
/// the transaction service: who/what/when, plus snapshot fields for ADR-007.
/// </summary>
public sealed record BookingRequest(
    Guid PatientId,
    Guid DentistId,
    Guid ServiceId,
    Guid? PublishedSessionId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    // Snapshot fields
    string ServiceNameSnapshot,
    int ServiceDurationMinutesSnapshot,
    decimal PriceAmountSnapshot,
    string CurrencySnapshot,
    string DentistDisplayNameSnapshot,
    int CancellationPolicyVersion,
    decimal CancellationFeePercent,
    decimal RefundablePercent,
    string? TermsSnapshotHash,
    // Resource requirements to allocate
    IReadOnlyList<ResourceRequirementItem> RequiredResources,
    string? CorrelationId = null,
    TimeSpan? PaymentWindow = null);

/// <summary>
/// A single resource requirement for the booking. The transaction service finds
/// a concrete ClinicResource matching the type and allocates it.
/// </summary>
public sealed record ResourceRequirementItem(
    int ResourceTypeValue,
    int QuantityRequired,
    bool RequireDentistOwned,
    Guid? SpecificResourceId);
