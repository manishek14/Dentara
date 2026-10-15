namespace Dentara.Domain.Exceptions.Identity;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a phone number is already registered to another user.
/// </summary>
public sealed class PhoneNumberAlreadyRegisteredException : DomainException
{
    public string PhoneNumber { get; }

    public PhoneNumberAlreadyRegisteredException(string phoneNumber)
        : base(
            "IDENTITY_PHONE_ALREADY_REGISTERED",
            $"Phone number {phoneNumber} is already registered.")
    {
        PhoneNumber = phoneNumber;
    }
}
