namespace Dentara.Domain.Booking;

using Dentara.Domain.Common;

/// <summary>
/// The appointment entity — central to the Booking module.
///
/// CRITICAL INVARIANTS:
/// - StartAtUtc < EndAtUtc (always)
/// - All snapshot fields (ServiceName, PriceAmount, DurationMinutes, etc.) are
///   captured at Create() time and never mutated (ADR-007).
/// - State transitions are guarded by TransitionTo() — illegal transitions throw.
/// - The 10-minute payment window is enforced via ExpiresAtUtc. The actual
///   expiry processing happens in a Background Worker, NOT in this entity.
///
/// This entity does NOT contain resource allocation logic. That lives in
/// ResourceAllocation rows, created atomically within the booking transaction
/// (ADR-003).
/// </summary>
public sealed class Appointment : Entity
{
    public Guid PatientId { get; private set; }

    public Guid DentistId { get; private set; }

    /// <summary>
    /// Reference to the service that was booked. Cross-module ID reference
    /// (no navigation property, per ADR-002).
    /// </summary>
    public Guid ServiceId { get; private set; }

    /// <summary>
    /// Optional link to the PublishedSession this appointment was booked against.
    /// Null if booked outside the publication system (e.g. walk-in).
    /// </summary>
    public Guid? PublishedSessionId { get; private set; }

    public DateTimeOffset StartAtUtc { get; private set; }

    public DateTimeOffset EndAtUtc { get; private set; }

    public int StatusValue { get; private set; }

    public AppointmentStatus Status => AppointmentStatus.FromValue(StatusValue);

    /// <summary>
    /// When the PendingPayment window expires. Set at creation.
    /// For Confirmed appointments, this field is informational (the actual
    /// expiry worker should skip confirmed rows).
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    // --- Snapshot fields (ADR-007) — never mutate after creation ---
    public string ServiceNameSnapshot { get; private set; } = string.Empty;
    public int ServiceDurationMinutesSnapshot { get; private set; }
    public decimal PriceAmountSnapshot { get; private set; }
    public string CurrencySnapshot { get; private set; } = "IRR";
    public string DentistDisplayNameSnapshot { get; private set; } = string.Empty;
    public int CancellationPolicyVersionSnapshot { get; private set; }
    public decimal CancellationFeePercentSnapshot { get; private set; }
    public decimal RefundablePercentSnapshot { get; private set; }
    public string? TermsSnapshotHash { get; private set; }

    // --- Lifecycle timestamps ---
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? ExpiredAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? NoShowAtUtc { get; private set; }

    /// <summary>
    /// Who cancelled, if applicable. "Patient", "Staff:Ali", "System:Expiry".
    /// </summary>
    public string? CancelledBy { get; private set; }

    public string? CancellationReason { get; private set; }

    private readonly List<ResourceAllocation> _resourceAllocations = new();
    public IReadOnlyCollection<ResourceAllocation> ResourceAllocations => _resourceAllocations.AsReadOnly();

    private readonly List<AppointmentEvent> _events = new();
    public IReadOnlyCollection<AppointmentEvent> Events => _events.AsReadOnly();

    private Appointment() { } // EF Core

    public TimeRange TimeRange => new(StartAtUtc, EndAtUtc);

    /// <summary>
    /// Factory method. Captures all commercial snapshots (ADR-007).
    /// </summary>
    public static Appointment Create(
        Guid patientId,
        Guid dentistId,
        Guid serviceId,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        IClock clock,
        // Snapshot inputs
        string serviceNameSnapshot,
        int serviceDurationMinutesSnapshot,
        decimal priceAmountSnapshot,
        string currencySnapshot,
        string dentistDisplayNameSnapshot,
        int cancellationPolicyVersion,
        decimal cancellationFeePercent,
        decimal refundablePercent,
        string? termsSnapshotHash = null,
        Guid? publishedSessionId = null,
        TimeSpan? paymentWindow = null)
    {
        if (patientId == Guid.Empty) throw new ArgumentException("PatientId required.", nameof(patientId));
        if (dentistId == Guid.Empty) throw new ArgumentException("DentistId required.", nameof(dentistId));
        if (serviceId == Guid.Empty) throw new ArgumentException("ServiceId required.", nameof(serviceId));
        if (startAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("StartAt must be UTC.", nameof(startAtUtc));
        if (endAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("EndAt must be UTC.", nameof(endAtUtc));
        if (startAtUtc >= endAtUtc) throw new ArgumentException("StartAt must be before EndAt.");
        if (string.IsNullOrWhiteSpace(serviceNameSnapshot)) throw new ArgumentException("ServiceNameSnapshot required.");
        if (serviceDurationMinutesSnapshot <= 0) throw new ArgumentException("Duration must be positive.");
        if (priceAmountSnapshot < 0) throw new ArgumentException("Price must be non-negative.");
        if (string.IsNullOrWhiteSpace(currencySnapshot) || currencySnapshot.Length != 3)
            throw new ArgumentException("Currency must be ISO 4217.");
        if (string.IsNullOrWhiteSpace(dentistDisplayNameSnapshot)) throw new ArgumentException("DentistDisplayNameSnapshot required.");
        if (cancellationFeePercent < 0 || cancellationFeePercent > 100) throw new ArgumentException("Fee must be in [0,100].");
        if (refundablePercent < 0 || refundablePercent > 100) throw new ArgumentException("Refundable must be in [0,100].");
        if (Math.Abs(cancellationFeePercent + refundablePercent - 100m) > 0.01m)
            throw new ArgumentException("Fee + Refundable must equal 100.");

        var window = paymentWindow ?? TimeSpan.FromMinutes(10);
        if (window <= TimeSpan.Zero) throw new ArgumentException("Payment window must be positive.");

        var appt = new Appointment
        {
            PatientId = patientId,
            DentistId = dentistId,
            ServiceId = serviceId,
            PublishedSessionId = publishedSessionId,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            StatusValue = AppointmentStatus.PendingPayment.Value,
            ExpiresAtUtc = clock.UtcNow + window,

            ServiceNameSnapshot = serviceNameSnapshot,
            ServiceDurationMinutesSnapshot = serviceDurationMinutesSnapshot,
            PriceAmountSnapshot = priceAmountSnapshot,
            CurrencySnapshot = currencySnapshot.ToUpperInvariant(),
            DentistDisplayNameSnapshot = dentistDisplayNameSnapshot,
            CancellationPolicyVersionSnapshot = cancellationPolicyVersion,
            CancellationFeePercentSnapshot = cancellationFeePercent,
            RefundablePercentSnapshot = refundablePercent,
            TermsSnapshotHash = termsSnapshotHash,

            CreatedAtUtc = clock.UtcNow
        };
        appt.RecordEvent(AppointmentEventType.Created, clock, "Appointment created");
        appt.RecordEvent(AppointmentEventType.PaymentWindowStarted, clock,
            $"Payment window expires at {appt.ExpiresAtUtc:O}");
        return appt;
    }

    /// <summary>
    /// Transition the appointment to Confirmed. Only legal from PendingPayment.
    /// </summary>
    public void Confirm(IClock clock, string reason = "Payment succeeded")
    {
        EnsureTransitionAllowed(AppointmentStatus.PendingPayment, AppointmentStatus.Confirmed);
        StatusValue = AppointmentStatus.Confirmed.Value;
        ConfirmedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
        RecordEvent(AppointmentEventType.Confirmed, clock, reason);
    }

    /// <summary>
    /// Transition to Expired. Only legal from PendingPayment. The expiry worker
    /// must first verify that no payment is in Unknown/Succeeded state (ADR-004).
    /// </summary>
    public void Expire(IClock clock, string reason = "Payment window expired")
    {
        EnsureTransitionAllowed(AppointmentStatus.PendingPayment, AppointmentStatus.Expired);
        StatusValue = AppointmentStatus.Expired.Value;
        ExpiredAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
        RecordEvent(AppointmentEventType.Expired, clock, reason);
    }

    /// <summary>
    /// Transition to Cancelled. Legal from PendingPayment or Confirmed.
    /// Caller must release capacity atomically (ResourceAllocations) in the
    /// same DB transaction (ADR-003).
    /// </summary>
    public void Cancel(IClock clock, string cancelledBy, string? reason = null)
    {
        if (Status != AppointmentStatus.PendingPayment && Status != AppointmentStatus.Confirmed)
            throw new InvalidOperationException(
                $"Cannot cancel an appointment in status {Status}.");

        StatusValue = AppointmentStatus.Cancelled.Value;
        CancelledAtUtc ??= clock.UtcNow;
        CancelledBy = cancelledBy;
        CancellationReason = reason;
        Touch(clock.UtcNow);
        RecordEvent(AppointmentEventType.Cancelled, clock,
            $"Cancelled by {cancelledBy}. Reason: {reason ?? "(none)"}");
    }

    /// <summary>
    /// Mark as Completed after the service was delivered.
    /// </summary>
    public void Complete(IClock clock, string? notes = null)
    {
        EnsureTransitionAllowed(AppointmentStatus.Confirmed, AppointmentStatus.Completed);
        StatusValue = AppointmentStatus.Completed.Value;
        CompletedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
        RecordEvent(AppointmentEventType.Completed, clock, notes ?? "Service delivered");
    }

    /// <summary>
    /// Mark as NoShow. The patient did not show up.
    /// </summary>
    public void MarkNoShow(IClock clock, string? notes = null)
    {
        EnsureTransitionAllowed(AppointmentStatus.Confirmed, AppointmentStatus.NoShow);
        StatusValue = AppointmentStatus.NoShow.Value;
        NoShowAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
        RecordEvent(AppointmentEventType.NoShow, clock, notes ?? "Patient did not arrive");
    }

    /// <summary>
    /// Add a resource allocation to this appointment. Must be called within the
    /// booking transaction (ADR-003).
    /// </summary>
    public void AddResourceAllocation(ResourceAllocation allocation)
    {
        if (allocation.AppointmentId != Id)
            throw new ArgumentException("Allocation does not belong to this appointment.");
        _resourceAllocations.Add(allocation);
    }

    /// <summary>
    /// Records an event in the audit trail. Internal — called by state transitions.
    /// </summary>
    internal AppointmentEvent RecordEvent(AppointmentEventType type, IClock clock, string? notes = null)
    {
        var evt = AppointmentEvent.Create(Id, type, clock, notes);
        _events.Add(evt);
        return evt;
    }

    /// <summary>
    /// Compute the refund amount based on the snapshotted refundable percent.
    /// </summary>
    public decimal CalculateRefundAmount() =>
        PriceAmountSnapshot * (RefundablePercentSnapshot / 100m);

    /// <summary>
    /// Compute the cancellation fee based on the snapshotted fee percent.
    /// </summary>
    public decimal CalculateCancellationFee() =>
        PriceAmountSnapshot * (CancellationFeePercentSnapshot / 100m);

    private void EnsureTransitionAllowed(AppointmentStatus from, AppointmentStatus to)
    {
        if (Status != from)
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {to}. Expected source state: {from}.");
    }
}
