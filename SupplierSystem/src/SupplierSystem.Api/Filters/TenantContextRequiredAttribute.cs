using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TenantContextRequiredAttribute : Attribute, IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.GetAuthUser();
        if (user == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Authentication required." });
            return Task.CompletedTask;
        }

        var tenantId = context.HttpContext.GetTenantId();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return next();
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new BadRequestObjectResult(new
            {
                message = "Tenant context required. Super administrators must target a tenant via the x-tenant-id header or tenantId query parameter."
            });
            return Task.CompletedTask;
        }

        context.Result = new ObjectResult(new { message = "Tenant association missing for current user." })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };

        return Task.CompletedTask;
    }
}
