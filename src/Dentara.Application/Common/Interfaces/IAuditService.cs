namespace Dentara.Application.Common.Interfaces;

/// <summary>
/// Service for recording audit log entries. Called by command handlers and
/// domain services when sensitive changes occur.
///
/// Per the architecture spec:
///   "اصلاح پرونده، تغییر تعرفه، لغو نوبت و تغییر مجوزها باید قابل پیگیری باشد.
///    حذف یا ویرایش اطلاعات حساس نباید ردپای لازم برای حسابرسی را از بین ببرد."
/// </summary>
public interface IAuditService
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable audit entry — describes a single sensitive change.
/// </summary>
public sealed record AuditEntry(
    string EntityType,
    Guid EntityId,
    string Action,
    string PerformedBy,
    DateTimeOffset OccurredAtUtc,
    string? Description = null,
    Guid? PerformedByUserId = null,
    string? OldValueJson = null,
    string? NewValueJson = null,
    string? IpAddress = null,
    string? CorrelationId = null);
