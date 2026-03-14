using System.Text.Json;
using System.Text.RegularExpressions;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;

namespace SupplierSystem.Api.Middleware;

public sealed class LockdownGuardMiddleware
{
    private static readonly Regex[] WhitelistPatterns =
    {
        new("^/$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/health(?:/.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/system/lockdown(?:/.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/auth/login/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/auth/invitation/.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    private static readonly Regex[] DownloadPatterns =
    {
        new("^/uploads(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/files/download/.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/suppliers/[^/]+/documents/[^/]+/download$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/requisitions/[^/]+/attachments/[^/]+/download$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new("^/api/invoices/[^/]+/download$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "OPTIONS",
        "HEAD",
    };

    private static readonly HashSet<string> DownloadQueryHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "download",
        "export",
        "exporttype",
        "format",
    };

    private readonly RequestDelegate _next;

    public LockdownGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SystemLockdownService lockdownService)
    {
        var status = await lockdownService.GetStatusAsync(context.RequestAborted);
        if (!status.IsActive)
        {
            await _next(context);
            return;
        }

        if (IsWhitelisted(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (SafeMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var user = context.GetAuthUser();
        if (user != null && string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
            !IsDownloadRequest(context))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/json";

        var payload = new
        {
            error = "Service Temporarily Unavailable",
            message = status.Announcement ?? "System is currently in read-only mode due to maintenance.",
            lockdown = new
            {
                isActive = true,
                announcement = status.Announcement,
                activatedAt = status.ActivatedAt,
            },
            retryAfter = 60,
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static bool IsWhitelisted(PathString path)
    {
        var value = path.Value ?? "/";
        return WhitelistPatterns.Any(pattern => pattern.IsMatch(value));
    }

    private static bool IsDownloadRequest(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (DownloadPatterns.Any(pattern => pattern.IsMatch(path)))
        {
            return true;
        }

        foreach (var key in context.Request.Query.Keys)
        {
            if (DownloadQueryHints.Contains(key))
            {
                return true;
            }
        }

        var accept = context.Request.Headers.Accept.ToString();
        if (!string.IsNullOrWhiteSpace(accept) &&
            accept.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
