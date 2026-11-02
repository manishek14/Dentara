namespace Dentara.Infrastructure.Users;

using Dentara.Application.Common.Results;
using Dentara.Application.Users.Queries;
using Dentara.Domain.Identity;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class UserQueryHandlers
    : IRequestHandler<GetUsersQuery, Result<PagedResult<UserDto>>>,
      IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly DentaraDbContext _db;

    public UserQueryHandlers(DentaraDbContext db) => _db = db;

    public async Task<Result<PagedResult<UserDto>>> Handle(
        GetUsersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _db.Users
                .Include(u => u.Roles)
                .Include(u => u.PatientProfiles)
                .Include(u => u.DentistProfiles)
                .AsQueryable();

            if (request.IsActive.HasValue)
                query = query.Where(u => u.IsActive == request.IsActive.Value);

            var allUsers = await query.ToListAsync(cancellationToken);

            // Filter by role if specified (client-side because RoleValue is int)
            if (!string.IsNullOrEmpty(request.RoleName))
            {
                try
                {
                    var roleValue = Role.FromName(request.RoleName).Value;
                    allUsers = allUsers.Where(u => u.Roles.Any(r => r.RoleValue == roleValue)).ToList();
                }
                catch (ArgumentException)
                {
                    return Result<PagedResult<UserDto>>.Success(
                        PagedResult<UserDto>.Empty(request.PageNumber, request.PageSize));
                }
            }

            var totalCount = allUsers.Count;
            var pageSize = Math.Max(1, Math.Min(request.PageSize, 200));
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageItems = allUsers
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            return Result<PagedResult<UserDto>>.Success(
                new PagedResult<UserDto>(pageItems, totalCount, pageNumber, pageSize));
        }
        catch (Exception ex) { return Result<PagedResult<UserDto>>.FromException(ex); }
    }

    public async Task<Result<UserDto>> Handle(
        GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _db.Users
                .Include(u => u.Roles)
                .Include(u => u.PatientProfiles)
                .Include(u => u.DentistProfiles)
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user is null)
                return Result<UserDto>.Failure("NOTFOUND_USER", $"User {request.UserId} not found.");

            return Result<UserDto>.Success(MapToDto(user));
        }
        catch (Exception ex) { return Result<UserDto>.FromException(ex); }
    }

    private static UserDto MapToDto(Domain.Identity.User u)
    {
        var roles = u.Roles.Select(r => Role.FromValue(r.RoleValue).Name).ToList();
        var patientProfile = u.PatientProfiles.FirstOrDefault();
        var dentistProfile = u.DentistProfiles.FirstOrDefault();

        return new UserDto(
            u.Id,
            u.PhoneNumber,
            u.IsActive,
            u.IsPhoneVerified,
            u.LastLoginAtUtc,
            u.CreatedAtUtc,
            roles,
            patientProfile is null ? null : new PatientProfileDto(
                patientProfile.Id, patientProfile.FirstName, patientProfile.LastName,
                patientProfile.FullName, patientProfile.NationalCode, patientProfile.DateOfBirth),
            dentistProfile is null ? null : new DentistProfileDto(
                dentistProfile.Id, dentistProfile.DisplayName, dentistProfile.Specialization,
                dentistProfile.LicenseNumber, dentistProfile.IsActive));
    }
}
