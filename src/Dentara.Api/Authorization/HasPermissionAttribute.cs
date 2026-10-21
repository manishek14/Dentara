namespace Dentara.Api.Authorization;

using Dentara.Application.Common.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

/// <summary>
/// Attribute that enforces a specific permission on a controller action.
/// The check is performed in the Backend (not just the UI) per the
/// architecture spec:
///   "محدودیت دسترسی باید در Backend اعمال شود، نه فقط در رابط کاربری."
///
/// Usage:
///   [HasPermission(Permissions.AppointmentCancelAny)]
///   public async Task<IActionResult> CancelAny(...)
///
/// The attribute checks the current user's roles against the
/// RolePermissions mapping. If the user doesn't have the required
/// permission, returns 403 Forbidden.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;

    public HasPermissionAttribute(string permission)
    {
        _permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get the user's roles from claims
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Count == 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        // Check if any of the user's roles grant the required permission
        var hasPermission = roles
            .SelectMany(RolePermissions.GetForRole)
            .Contains(_permission);

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new
            {
                errorCode = "FORBIDDEN",
                message = $"Permission '{_permission}' is required for this action."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
