using Microsoft.AspNetCore.Authorization;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
        {
            return Task.CompletedTask;
        }

        var user = httpContext.GetAuthUser();
        if (user == null)
        {
            return Task.CompletedTask;
        }

        if (requirement.AllowAdminRole &&
            string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (user.Permissions == null || requirement.Permissions.Count == 0)
        {
            return Task.CompletedTask;
        }

        var granted = new HashSet<string>(user.Permissions, StringComparer.OrdinalIgnoreCase);
        if (requirement.Permissions.Any(granted.Contains))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
