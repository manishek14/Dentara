namespace Dentara.Domain.Events.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Raised when an OTP is requested (login, registration, password reset).
/// Side effects: dispatch SMS via sms.ir gateway (rate-limited by IdempotencyRecord).
/// </summary>
public sealed record OtpRequestedEvent(
    Guid UserId,
    string PhoneNumber,
    string Purpose, // "login", "register", "reset_password"
    string OtpCode, // hashed/encrypted by Application layer before dispatch
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(UserId, OccurredAtUtc, CorrelationId);
