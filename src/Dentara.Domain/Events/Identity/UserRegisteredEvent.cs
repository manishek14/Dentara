namespace Dentara.Domain.Events.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a new user account is created.
/// Side effects: send OTP for phone verification.
/// </summary>
public sealed record UserRegisteredEvent(
    Guid UserId,
    string PhoneNumber,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(UserId, OccurredAtUtc, CorrelationId);
