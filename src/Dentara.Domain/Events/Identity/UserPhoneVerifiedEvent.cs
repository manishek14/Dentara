namespace Dentara.Domain.Events.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a user's phone number is verified (OTP matched).
/// Side effects: mark user as verified, allow booking operations.
/// </summary>
public sealed record UserPhoneVerifiedEvent(
    Guid UserId,
    string PhoneNumber,
    DateTimeOffset VerifiedAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(UserId, OccurredAtUtc, CorrelationId);
