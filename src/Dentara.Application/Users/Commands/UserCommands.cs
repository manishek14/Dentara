namespace Dentara.Application.Users.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Identity;
using MediatR;

// ─── User Status ───

public sealed record DeactivateUserCommand(
    Guid UserId,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record ActivateUserCommand(
    Guid UserId,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

// ─── Role Assignment ───

public sealed record AssignRoleCommand(
    Guid UserId,
    string RoleName,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record RevokeRoleCommand(
    Guid UserId,
    string RoleName,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

// ─── Profile Updates ───

public sealed record UpdatePatientProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? NationalCode,
    DateTimeOffset? DateOfBirth,
    string? Notes,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record UpdateDentistProfileCommand(
    Guid UserId,
    string DisplayName,
    string? Specialization,
    string? LicenseNumber,
    string? Bio,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}
