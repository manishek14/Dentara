namespace Dentara.Domain.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Allocation of a specific ClinicResource to an Appointment for a specific
/// time range. Multiple rows per appointment when a service requires multiple
/// resources (e.g. dentist + dental unit + assistant).
///
/// CRITICAL: rows are created atomically within the booking transaction (ADR-003).
/// The SQL Server overlap check uses this table:
///
///   SELECT COUNT(*) FROM ResourceAllocations WITH (HOLDLOCK)
///   WHERE ResourceId = @resourceId
///     AND Status IN ('Active')
///     AND StartAtUtc < @newEnd
///     AND EndAtUtc > @newStart;
///
/// If COUNT + new_quantity > resource.Capacity, the transaction rolls back.
/// </summary>
public sealed class ResourceAllocation : Entity
{
    public Guid AppointmentId { get; private set; }

    public Guid ResourceId { get; private set; }

    /// <summary>
    /// Optional: the resource type value at allocation time. Useful for queries
    /// by type without joining to ClinicResource (which may have been deactivated).
    /// </summary>
    public int ResourceTypeSnapshotValue { get; private set; }

    public DateTimeOffset StartAtUtc { get; private set; }

    public DateTimeOffset EndAtUtc { get; private set; }

    public int StatusValue { get; private set; }

    public AllocationStatus Status => AllocationStatus.FromValue(StatusValue);

    public DateTimeOffset? ReleasedAtUtc { get; private set; }

    public string? ReleaseReason { get; private set; }

    private ResourceAllocation() { } // EF Core

    public TimeRange TimeRange => new(StartAtUtc, EndAtUtc);

    internal static ResourceAllocation Create(
        Guid appointmentId,
        Guid resourceId,
        int resourceTypeValue,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        IClock clock)
    {
        if (appointmentId == Guid.Empty) throw new ArgumentException("AppointmentId required.");
        if (resourceId == Guid.Empty) throw new ArgumentException("ResourceId required.");
        if (startAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("StartAt must be UTC.");
        if (endAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("EndAt must be UTC.");
        if (startAtUtc >= endAtUtc) throw new ArgumentException("StartAt must be before EndAt.");

        return new ResourceAllocation
        {
            AppointmentId = appointmentId,
            ResourceId = resourceId,
            ResourceTypeSnapshotValue = resourceTypeValue,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            StatusValue = AllocationStatus.Active.Value,
            CreatedAtUtc = clock.UtcNow
        };
    }

    /// <summary>
    /// Release this allocation. Idempotent: if already released, no-op.
    /// Called when the parent appointment is cancelled or expired.
    /// </summary>
    public void Release(IClock clock, string? reason = null)
    {
        if (Status == AllocationStatus.Released) return;
        if (Status != AllocationStatus.Active)
            throw new InvalidOperationException($"Cannot release allocation in status {Status}.");

        StatusValue = AllocationStatus.Released.Value;
        ReleasedAtUtc ??= clock.UtcNow;
        ReleaseReason = reason;
        Touch(clock.UtcNow);
    }
}

/// <summary>
/// Lifecycle of a resource allocation. Released allocations remain in the table
/// for audit but are excluded from active overlap queries.
/// </summary>
public sealed class AllocationStatus : Enumeration
{
    /// <summary>
    /// Allocation is consuming the resource's capacity.
    /// </summary>
    public static readonly AllocationStatus Active = new(1, nameof(Active));

    /// <summary>
    /// Allocation has been released (appointment cancelled/expired/completed).
    /// No longer consumes capacity.
    /// </summary>
    public static readonly AllocationStatus Released = new(2, nameof(Released));

    private AllocationStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<AllocationStatus> All() => GetAll<AllocationStatus>();

    public static AllocationStatus FromValue(int value) => FromValue<AllocationStatus>(value);

    public static AllocationStatus FromName(string name) => FromName<AllocationStatus>(name);
}
