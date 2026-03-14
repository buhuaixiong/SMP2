using Microsoft.AspNetCore.Authorization;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Authorization;

public sealed class BuyerAssignmentsReadHandler : AuthorizationHandler<BuyerAssignmentsReadRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BuyerAssignmentsReadRequirement requirement)
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

        if (HasAdminPermission(user))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var buyerId = httpContext.Request.Query["buyerId"].ToString();
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            buyerId = user.Id;
        }

        if (string.Equals(buyerId, user.Id, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static bool HasAdminPermission(Application.Models.Auth.AuthUser user)
    {
        if (user.Permissions == null)
        {
            return false;
        }

        var granted = new HashSet<string>(user.Permissions, StringComparer.OrdinalIgnoreCase);
        return granted.Contains(Permissions.AdminBuyerAssignmentsManage);
    }
}
