using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Middleware;

public sealed class TenantContextMiddleware
{
    private const string TenantHeader = "x-tenant-id";
    private const string TenantQuery = "tenantId";
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = ResolveTenantId(context);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            context.Items["TenantId"] = tenantId;
        }

        await _next(context);
    }

    private static string? ResolveTenantId(HttpContext context)
    {
        var user = context.GetAuthUser();
        if (user == null)
        {
            return null;
        }

        var hasTenant = !string.IsNullOrWhiteSpace(user.TenantId);
        var isSuperAdmin = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase) && !hasTenant;

        if (isSuperAdmin)
        {
            var overrideTenant = GetTenantOverride(context);
            if (!string.IsNullOrWhiteSpace(overrideTenant))
            {
                return overrideTenant;
            }

            return ResolveSuperAdminFallbackTenant();
        }

        if (hasTenant)
        {
            return user.TenantId;
        }

        return null;
    }

    private static string? GetTenantOverride(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TenantHeader, out var headerValues))
        {
            var headerTenant = headerValues.ToString();
            if (!string.IsNullOrWhiteSpace(headerTenant))
            {
                return headerTenant.Trim();
            }
        }

        if (context.Request.Query.TryGetValue(TenantQuery, out var queryValues))
        {
            var queryTenant = queryValues.ToString();
            if (!string.IsNullOrWhiteSpace(queryTenant))
            {
                return queryTenant.Trim();
            }
        }

        return null;
    }

    private static string ResolveSuperAdminFallbackTenant()
    {
        var fallback = Environment.GetEnvironmentVariable("SUPERADMIN_FALLBACK_TENANT_ID");
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        fallback = Environment.GetEnvironmentVariable("DEFAULT_TENANT_ID");
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback.Trim();
        }

        return "tenant-default";
    }
}
