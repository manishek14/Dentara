namespace Dentara.Domain.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Patient profile. A user can have multiple patient profiles only if the clinic
/// supports family accounts (out of v1 scope — kept as collection for future).
/// For v1, one patient profile per user.
/// </summary>
public sealed class PatientProfile : Entity
{
    public Guid UserId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// National code (Iranian کد ملی). Optional; 10 digits.
    /// </summary>
    public string? NationalCode { get; private set; }

    public DateTimeOffset? DateOfBirth { get; private set; }

    public string? Notes { get; private set; }

    private PatientProfile() { } // EF Core

    public static PatientProfile Create(
        Guid userId,
        string firstName,
        string lastName,
        IClock clock,
        string? nationalCode = null,
        DateTimeOffset? dateOfBirth = null,
        string? notes = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (!string.IsNullOrEmpty(nationalCode) && !IsValidNationalCode(nationalCode))
            throw new ArgumentException("National code must be 10 digits.", nameof(nationalCode));

        return new PatientProfile
        {
            UserId = userId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            NationalCode = nationalCode,
            DateOfBirth = dateOfBirth,
            Notes = notes,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void Update(
        string firstName,
        string lastName,
        IClock clock,
        string? nationalCode = null,
        DateTimeOffset? dateOfBirth = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));
        if (!string.IsNullOrEmpty(nationalCode) && !IsValidNationalCode(nationalCode))
            throw new ArgumentException("National code must be 10 digits.", nameof(nationalCode));

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        NationalCode = nationalCode;
        DateOfBirth = dateOfBirth;
        Notes = notes;
        Touch(clock.UtcNow);
    }

    private static bool IsValidNationalCode(string code) =>
        code.Length == 10 && code.All(char.IsDigit);
}
