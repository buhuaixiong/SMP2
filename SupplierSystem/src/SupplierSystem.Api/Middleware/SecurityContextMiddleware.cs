using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Models.Security;

namespace SupplierSystem.Api.Middleware;

public sealed class SecurityContextMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientIp = ResolveClientIp(context);
        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            context.Items["ClientIp"] = clientIp;
        }

        var securityContext = new RequestSecurityContext(clientIp, context.GetAuthUser());
        context.Items["SecurityContext"] = securityContext;

        await _next(context);
    }

    private static string? ResolveClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
        var fromForwardedFor = GetFirstForwardedIp(forwardedFor);
        if (!string.IsNullOrWhiteSpace(fromForwardedFor))
        {
            return fromForwardedFor;
        }

        var realIp = context.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return NormalizeIp(realIp);
        }

        var forwarded = context.Request.Headers["Forwarded"].ToString();
        var fromForwardedHeader = ParseForwardedHeader(forwarded);
        if (!string.IsNullOrWhiteSpace(fromForwardedHeader))
        {
            return fromForwardedHeader;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? GetFirstForwardedIp(string? forwardedFor)
    {
        if (string.IsNullOrWhiteSpace(forwardedFor))
        {
            return null;
        }

        var first = forwardedFor.Split(',')[0].Trim();
        return NormalizeIp(first);
    }

    private static string? ParseForwardedHeader(string? forwarded)
    {
        if (string.IsNullOrWhiteSpace(forwarded))
        {
            return null;
        }

        var entries = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            var segments = entry.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (!segment.StartsWith("for=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = segment[4..].Trim().Trim('"');
                return NormalizeIp(value);
            }
        }

        return null;
    }

    private static string? NormalizeIp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.Contains(']'))
        {
            var end = trimmed.IndexOf(']');
            if (end > 0)
            {
                return trimmed.Substring(1, end - 1);
            }
        }

        var colonCount = 0;
        foreach (var ch in trimmed)
        {
            if (ch == ':')
            {
                colonCount++;
            }
        }

        if (colonCount == 1)
        {
            var lastColon = trimmed.LastIndexOf(':');
            if (lastColon > 0 && lastColon < trimmed.Length - 1)
            {
                var host = trimmed[..lastColon];
                var port = trimmed[(lastColon + 1)..];
                var digitsOnly = true;
                foreach (var ch in port)
                {
                    if (!char.IsDigit(ch))
                    {
                        digitsOnly = false;
                        break;
                    }
                }

                if (digitsOnly)
                {
                    return host;
                }
            }
        }

        return trimmed;
    }
}
