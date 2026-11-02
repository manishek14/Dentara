namespace Dentara.Api.Controllers;

using Dentara.Api.Authorization;
using Dentara.Api.Contracts.Common;
using Dentara.Api.Contracts.Users;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Common.Authorization;
using Dentara.Application.Users.Commands;
using Dentara.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// User management endpoints: list, get, activate/deactivate, role assignment,
/// profile updates.
///
/// Permission mapping:
///   user.manage  → Admin (activate/deactivate, update profiles)
///   role.assign  → Admin (assign/revoke roles)
///   (read endpoints: any authenticated user can view their own profile;
///    staff with user.manage can view all)
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMediator mediator, ILogger<UsersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // ─── Queries ───

    /// <summary>
    /// Lists all users, optionally filtered by role and active status.
    /// Patients can only see their own profile (forced filter).
    /// Staff with user.manage can see all.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? roleName,
        [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var canManageUsers = roles.Any(r =>
            RolePermissions.GetForRole(r).Contains(Permissions.UserManage));

        // Patients without user.manage can only see themselves
        if (!canManageUsers)
        {
            // Redirect to their own profile
            var selfQuery = new GetUserByIdQuery(userId, HttpContext.GetCorrelationId());
            var selfResult = await _mediator.Send(selfQuery, cancellationToken);
            return selfResult.ToActionResult(u => new
            {
                items = new[] { MapToResponse(u) },
                totalCount = 1,
                pageNumber = 1,
                pageSize = 1
            });
        }

        var query = new GetUsersQuery(roleName, isActive, pageNumber, pageSize,
            HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(r => new
        {
            items = r.Items.Select(MapToResponse).ToList(),
            totalCount = r.TotalCount,
            pageNumber = r.PageNumber,
            pageSize = r.PageSize,
            totalPages = r.TotalPages,
            hasNextPage = r.HasNextPage,
            hasPreviousPage = r.HasPreviousPage
        });
    }

    /// <summary>
    /// Gets a single user by ID.
    /// Patients can only view their own profile.
    /// Staff with user.manage can view any.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var canManageUsers = roles.Any(r =>
            RolePermissions.GetForRole(r).Contains(Permissions.UserManage));

        if (!canManageUsers && id != userId)
            return Forbid();

        var query = new GetUserByIdQuery(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(MapToResponse);
    }

    // ─── Status Management ───

    /// <summary>
    /// Deactivates a user account. The user can no longer log in or book.
    /// Permission: user.manage (Admin).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateUserCommand(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Reactivates a previously deactivated user account.
    /// Permission: user.manage (Admin).
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ActivateUserCommand(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    // ─── Role Assignment ───

    /// <summary>
    /// Assigns a role to a user. Idempotent — if the user already has the
    /// role, no error is returned.
    /// Permission: role.assign (Admin).
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    [HasPermission(Permissions.RoleAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(
        [FromRoute] Guid id,
        [FromBody] AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "RoleName is required."));

        var command = new AssignRoleCommand(id, request.RoleName, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Revokes a role from a user. Idempotent.
    /// Permission: role.assign (Admin).
    /// </summary>
    [HttpDelete("{id:guid}/roles/{roleName}")]
    [HasPermission(Permissions.RoleAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeRole(
        [FromRoute] Guid id,
        [FromRoute] string roleName,
        CancellationToken cancellationToken)
    {
        var command = new RevokeRoleCommand(id, roleName, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    // ─── Profile Updates ───

    /// <summary>
    /// Updates the patient profile for a user. Creates the profile if it
    /// doesn't exist.
    /// Patients can update their own; staff with user.manage can update any.
    /// </summary>
    [HttpPut("{id:guid}/patient-profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePatientProfile(
        [FromRoute] Guid id,
        [FromBody] UpdatePatientProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var canManageUsers = roles.Any(r =>
            RolePermissions.GetForRole(r).Contains(Permissions.UserManage));

        if (!canManageUsers && id != userId)
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "FirstName and LastName are required."));

        var command = new UpdatePatientProfileCommand(
            id, request.FirstName, request.LastName, request.NationalCode,
            request.DateOfBirth, request.Notes, HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates the dentist profile for a user. Creates the profile if it
    /// doesn't exist.
    /// Permission: user.manage (Admin).
    /// </summary>
    [HttpPut("{id:guid}/dentist-profile")]
    [HasPermission(Permissions.UserManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDentistProfile(
        [FromRoute] Guid id,
        [FromBody] UpdateDentistProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "DisplayName is required."));

        var command = new UpdateDentistProfileCommand(
            id, request.DisplayName, request.Specialization,
            request.LicenseNumber, request.Bio, HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    // ─── Helpers ───

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        return Guid.Parse(sub);
    }

    private static UserResponse MapToResponse(UserDto u) => new(
        u.Id, u.PhoneNumber, u.IsActive, u.IsPhoneVerified,
        u.LastLoginAtUtc, u.CreatedAtUtc, u.Roles,
        u.PatientProfile is null ? null : new PatientProfileResponse(
            u.PatientProfile.Id, u.PatientProfile.FirstName, u.PatientProfile.LastName,
            u.PatientProfile.FullName, u.PatientProfile.NationalCode, u.PatientProfile.DateOfBirth),
        u.DentistProfile is null ? null : new DentistProfileResponse(
            u.DentistProfile.Id, u.DentistProfile.DisplayName,
            u.DentistProfile.Specialization, u.DentistProfile.LicenseNumber,
            u.DentistProfile.IsActive));
}
