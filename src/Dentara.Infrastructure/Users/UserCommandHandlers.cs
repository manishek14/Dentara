namespace Dentara.Infrastructure.Users;

using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Application.Users.Commands;
using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

public sealed class UserCommandHandlers
    : IRequestHandler<DeactivateUserCommand, Result>,
      IRequestHandler<ActivateUserCommand, Result>,
      IRequestHandler<AssignRoleCommand, Result>,
      IRequestHandler<RevokeRoleCommand, Result>,
      IRequestHandler<UpdatePatientProfileCommand, Result>,
      IRequestHandler<UpdateDentistProfileCommand, Result>
{
    private readonly DentaraDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public UserCommandHandlers(
        DentaraDbContext db, IAuditService audit,
        ICurrentUserContext currentUser, IClock clock)
    {
        _db = db; _audit = audit; _currentUser = currentUser; _clock = clock;
    }

    public async Task<Result> Handle(DeactivateUserCommand r, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null)
            return Result.Failure("NOTFOUND_USER", $"User {r.UserId} not found.");

        var wasActive = user.IsActive;
        user.Deactivate(_clock);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry("User", user.Id, "Deactivate",
            _currentUser.UserId.HasValue ? $"User:{_currentUser.UserId}" : "System",
            _clock.UtcNow, $"User {user.PhoneNumber} deactivated",
            _currentUser.UserId,
            OldValueJson: JsonSerializer.Serialize(new { isActive = wasActive }),
            NewValueJson: JsonSerializer.Serialize(new { isActive = false }),
            CorrelationId: r.CorrelationId), ct);

        return Result.Success();
    }

    public async Task<Result> Handle(ActivateUserCommand r, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null)
            return Result.Failure("NOTFOUND_USER", $"User {r.UserId} not found.");

        var wasActive = user.IsActive;
        user.Activate(_clock);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry("User", user.Id, "Activate",
            _currentUser.UserId.HasValue ? $"User:{_currentUser.UserId}" : "System",
            _clock.UtcNow, $"User {user.PhoneNumber} activated",
            _currentUser.UserId,
            OldValueJson: JsonSerializer.Serialize(new { isActive = wasActive }),
            NewValueJson: JsonSerializer.Serialize(new { isActive = true }),
            CorrelationId: r.CorrelationId), ct);

        return Result.Success();
    }

    public async Task<Result> Handle(AssignRoleCommand r, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null)
            return Result.Failure("NOTFOUND_USER", $"User {r.UserId} not found.");

        Role role;
        try { role = Role.FromName(r.RoleName); }
        catch (ArgumentException) { return Result.Failure("VALIDATION_ERROR", $"Unknown role: {r.RoleName}"); }

        var hadRole = user.HasRole(role);
        user.AssignRole(role, _clock);
        await _db.SaveChangesAsync(ct);

        if (!hadRole)
        {
            await _audit.LogAsync(new AuditEntry("UserRole", user.Id, "RoleAssign",
                _currentUser.UserId.HasValue ? $"User:{_currentUser.UserId}" : "System",
                _clock.UtcNow, $"Role '{r.RoleName}' assigned to user {user.PhoneNumber}",
                _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new { userId = user.Id, role = r.RoleName }),
                CorrelationId: r.CorrelationId), ct);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(RevokeRoleCommand r, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == r.UserId, ct);
        if (user is null)
            return Result.Failure("NOTFOUND_USER", $"User {r.UserId} not found.");

        Role role;
        try { role = Role.FromName(r.RoleName); }
        catch (ArgumentException) { return Result.Failure("VALIDATION_ERROR", $"Unknown role: {r.RoleName}"); }

        var hadRole = user.HasRole(role);
        user.RevokeRole(role, _clock);
        await _db.SaveChangesAsync(ct);

        if (hadRole)
        {
            await _audit.LogAsync(new AuditEntry("UserRole", user.Id, "RoleRevoke",
                _currentUser.UserId.HasValue ? $"User:{_currentUser.UserId}" : "System",
                _clock.UtcNow, $"Role '{r.RoleName}' revoked from user {user.PhoneNumber}",
                _currentUser.UserId,
                OldValueJson: JsonSerializer.Serialize(new { userId = user.Id, role = r.RoleName }),
                CorrelationId: r.CorrelationId), ct);
        }

        return Result.Success();
    }

    public async Task<Result> Handle(UpdatePatientProfileCommand r, CancellationToken ct)
    {
        var profile = await _db.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == r.UserId, ct);

        if (profile is null)
        {
            // Create if doesn't exist
            profile = PatientProfile.Create(r.UserId, r.FirstName, r.LastName, _clock,
                r.NationalCode, r.DateOfBirth, r.Notes);
            await _db.PatientProfiles.AddAsync(profile, ct);
        }
        else
        {
            var oldName = profile.FullName;
            profile.Update(r.FirstName, r.LastName, _clock, r.NationalCode, r.DateOfBirth, r.Notes);
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry("PatientProfile", profile.Id, "Update",
            _currentUser.UserId.HasValue ? $"User:{_currentUser.UserId}" : "System",
            _clock.UtcNow, $"Patient profile updated for user {r.UserId}",
            _currentUser.UserId,
            NewValueJson: JsonSerializer.Serialize(new { firstName = r.FirstName, lastName = r.LastName, nationalCode = r.NationalCode }),
            CorrelationId: r.CorrelationId), ct);

        return Result.Success();
    }

    public async Task<Result> Handle(UpdateDentistProfileCommand r, CancellationToken ct)
    {
        var profile = await _db.DentistProfiles
            .FirstOrDefaultAsync(d => d.UserId == r.UserId, ct);

        if (profile is null)
        {
            profile = DentistProfile.Create(r.UserId, r.DisplayName, _clock,
                r.Specialization, r.LicenseNumber, r.Bio);
            await _db.DentistProfiles.AddAsync(profile, ct);
        }
        else
        {
            var oldName = profile.DisplayName;
            profile.Update(r.DisplayName, _clock, r.Specialization, r.LicenseNumber, r.Bio);
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(new AuditEntry("DentistProfile", profile.Id, "Update",
            _currentUser.UserId.HasValue ? $"User:{_currentUser.UserId}" : "System",
            _clock.UtcNow, $"Dentist profile updated for user {r.UserId}",
            _currentUser.UserId,
            NewValueJson: JsonSerializer.Serialize(new { displayName = r.DisplayName, specialization = r.Specialization, licenseNumber = r.LicenseNumber }),
            CorrelationId: r.CorrelationId), ct);

        return Result.Success();
    }
}
