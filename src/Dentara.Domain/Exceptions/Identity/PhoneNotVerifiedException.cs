namespace Dentara.Domain.Exceptions.Identity;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a user attempts an operation that requires phone verification
/// but their phone is not yet verified.
/// </summary>
public sealed class PhoneNotVerifiedException : DomainException
{
    public Guid UserId { get; }

    public PhoneNotVerifiedException(Guid userId)
        : base(
            "IDENTITY_PHONE_NOT_VERIFIED",
            $"User {userId} must verify phone number before this operation.",
            userId)
    {
        UserId = userId;
    }
}
