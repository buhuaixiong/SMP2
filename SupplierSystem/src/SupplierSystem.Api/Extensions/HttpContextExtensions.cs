using SupplierSystem.Api.Models.Security;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Extensions;

public static class HttpContextExtensions
{
    public static AuthUser? GetAuthUser(this HttpContext context)
    {
        if (context.Items.TryGetValue("AuthUser", out var value) && value is AuthUser user)
        {
            return user;
        }

        return null;
    }

    public static RequestSecurityContext? GetSecurityContext(this HttpContext context)
    {
        if (context.Items.TryGetValue("SecurityContext", out var value) && value is RequestSecurityContext securityContext)
        {
            return securityContext;
        }

        return null;
    }

    public static string? GetClientIp(this HttpContext context)
    {
        if (context.Items.TryGetValue("ClientIp", out var value) && value is string ip && !string.IsNullOrWhiteSpace(ip))
        {
            return ip;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    public static string? GetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out var value) && value is string tenantId && !string.IsNullOrWhiteSpace(tenantId))
        {
            return tenantId;
        }

        return context.GetAuthUser()?.TenantId;
    }
}
