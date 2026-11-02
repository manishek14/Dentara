namespace Dentara.Application.Users.Queries;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

public sealed record GetUsersQuery(
    string? RoleName = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 50,
    string? CorrelationIdHeader = null)
    : IQuery<PagedResult<UserDto>>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

public sealed record GetUserByIdQuery(
    Guid UserId,
    string? CorrelationIdHeader = null)
    : IQuery<UserDto>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

public sealed record UserDto(
    Guid Id,
    string PhoneNumber,
    bool IsActive,
    bool IsPhoneVerified,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> Roles,
    PatientProfileDto? PatientProfile,
    DentistProfileDto? DentistProfile);

public sealed record PatientProfileDto(
    Guid Id, string FirstName, string LastName, string FullName,
    string? NationalCode, DateTimeOffset? DateOfBirth);

public sealed record DentistProfileDto(
    Guid Id, string DisplayName, string? Specialization,
    string? LicenseNumber, bool IsActive);
