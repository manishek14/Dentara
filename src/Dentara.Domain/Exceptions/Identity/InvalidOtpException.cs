namespace Dentara.Domain.Exceptions.Identity;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when an OTP verification fails (wrong code, expired, or already used).
/// </summary>
public sealed class InvalidOtpException : DomainException
{
    public Guid UserId { get; }
    public string Reason { get; }

    public InvalidOtpException(Guid userId, string reason)
        : base(
            "IDENTITY_INVALID_OTP",
            $"OTP verification failed for user {userId}: {reason}.",
            userId)
    {
        UserId = userId;
        Reason = reason;
    }
}
