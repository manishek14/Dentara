namespace Dentara.Domain.Audit;

using Dentara.Domain.Common;

/// <summary>
/// Immutable audit log entry. Records every sensitive change in the system:
/// appointment cancellations, price changes, role assignments, profile edits,
/// permission changes, refund approvals, etc.
///
/// CRITICAL per the architecture spec:
///   "حذف یا ویرایش اطلاعات حساس نباید ردپای لازم برای حسابرسی را از بین ببرد."
///
/// This means:
///   - AuditLog entries are INSERT-only. No UPDATE or DELETE is ever allowed.
///   - The entity has no Update/Remove methods.
///   - The EF Core configuration maps the table with no update/delete stored
///     procedures (prevent accidental modification).
///   - For sensitive data (e.g. price changes), the OldValue and NewValue are
///     stored as JSON snapshots so the full before/after is preserved.
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>
    /// The type of entity that was changed (e.g. "Appointment", "Service", "User").
    /// </summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>
    /// The ID of the entity that was changed.
    /// </summary>
    public Guid EntityId { get; private set; }

    /// <summary>
    /// The action performed (e.g. "Create", "Update", "Cancel", "PriceChange",
    /// "RoleAssign", "RoleRevoke", "Delete").
    /// </summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>
    /// Who performed the action. Format: "User:{GUID}" or "System:{WorkerName}".
    /// </summary>
    public string PerformedBy { get; private set; } = string.Empty;

    /// <summary>
    /// The user ID of the actor (if authenticated). Null for system actions.
    /// </summary>
    public Guid? PerformedByUserId { get; private set; }

    /// <summary>
    /// JSON snapshot of the entity's state BEFORE the change (for updates).
    /// Null for creates.
    /// </summary>
    public string? OldValueJson { get; private set; }

    /// <summary>
    /// JSON snapshot of the entity's state AFTER the change (for updates).
    /// Null for deletes.
    /// </summary>
    public string? NewValueJson { get; private set; }

    /// <summary>
    /// Human-readable description of what changed.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// IP address of the actor (if available from HTTP context).
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Correlation id linking to the HTTP request / domain event.
    /// </summary>
    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AuditLog() { } // EF Core

    public static AuditLog Create(
        string entityType,
        Guid entityId,
        string action,
        string performedBy,
        DateTimeOffset occurredAtUtc,
        string? description = null,
        Guid? performedByUserId = null,
        string? oldValueJson = null,
        string? newValueJson = null,
        string? ipAddress = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("EntityType required.");
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action required.");
        if (string.IsNullOrWhiteSpace(performedBy)) throw new ArgumentException("PerformedBy required.");
        if (occurredAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("OccurredAtUtc must be UTC.");

        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            PerformedBy = performedBy,
            PerformedByUserId = performedByUserId,
            OldValueJson = oldValueJson,
            NewValueJson = newValueJson,
            Description = description ?? string.Empty,
            IpAddress = ipAddress,
            CorrelationId = correlationId,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = occurredAtUtc
        };
    }
}
