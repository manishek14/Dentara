namespace Dentara.Domain.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Dentist profile. Holds the public-facing display name, specialization,
/// and whether the dentist is currently accepting bookings.
///
/// Note: the dentist's schedule lives in the Scheduling module (WorkSchedule,
/// ScheduleException). The dentist's services live in the Catalog module
/// (DentistService). This module holds identity only.
/// </summary>
public sealed class DentistProfile : Entity
{
    public Guid UserId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Specialization, e.g. "Orthodontist", "Oral Surgeon", "General Dentist".
    /// Free-text for v1; could become a reference table later.
    /// </summary>
    public string? Specialization { get; private set; }

    /// <summary>
    /// Medical license number. Required by Iranian regulation.
    /// </summary>
    public string? LicenseNumber { get; private set; }

    public bool IsActive { get; private set; }

    public string? Bio { get; private set; }

    private DentistProfile() { } // EF Core

    public static DentistProfile Create(
        Guid userId,
        string displayName,
        IClock clock,
        string? specialization = null,
        string? licenseNumber = null,
        string? bio = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        return new DentistProfile
        {
            UserId = userId,
            DisplayName = displayName.Trim(),
            Specialization = specialization,
            LicenseNumber = licenseNumber,
            Bio = bio,
            IsActive = true,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void Update(
        string displayName,
        IClock clock,
        string? specialization = null,
        string? licenseNumber = null,
        string? bio = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        DisplayName = displayName.Trim();
        Specialization = specialization;
        LicenseNumber = licenseNumber;
        Bio = bio;
        Touch(clock.UtcNow);
    }

    public void Deactivate(IClock clock)
    {
        IsActive = false;
        Touch(clock.UtcNow);
    }

    public void Activate(IClock clock)
    {
        IsActive = true;
        Touch(clock.UtcNow);
    }
}
