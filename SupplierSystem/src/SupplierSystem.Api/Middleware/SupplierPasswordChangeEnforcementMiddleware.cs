using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Models.Common;

namespace SupplierSystem.Api.Middleware;

public sealed class SupplierPasswordChangeEnforcementMiddleware
{
    private static readonly HashSet<string> SupplierRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "temp_supplier",
        "formal_supplier",
        "supplier",
    };

    private static readonly HashSet<string> ExemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/me",
        "/api/auth/change-password",
        "/api/auth/logout",
    };

    private readonly RequestDelegate _next;

    public SupplierPasswordChangeEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.GetAuthUser();
        if (user == null ||
            !user.MustChangePassword ||
            !SupplierRoles.Contains(user.Role) ||
            IsExemptRequest(context.Request))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Success = false,
            Error = "Password change is required before accessing this resource.",
            Code = "PASSWORD_CHANGE_REQUIRED",
        });
    }

    private static bool IsExemptRequest(HttpRequest request)
    {
        if (!request.Path.HasValue)
        {
            return false;
        }

        return ExemptPaths.Contains(request.Path.Value!);
    }
}
