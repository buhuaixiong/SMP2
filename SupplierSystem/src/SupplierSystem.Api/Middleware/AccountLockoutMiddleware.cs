using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Common;

namespace SupplierSystem.Api.Middleware;

/// <summary>
/// 账户锁定中间件 - 检查登录请求的账户锁定状态
/// </summary>
public sealed class AccountLockoutMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _loginPath;
    private readonly ILogger<AccountLockoutMiddleware> _logger;

    public AccountLockoutMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<AccountLockoutMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        // 从配置读取登录路径，支持自定义
        _loginPath = configuration["Auth:LoginPath"] ?? "/api/auth/login";
    }

    public async Task InvokeAsync(HttpContext context, IAccountLockoutService lockoutService)
    {
        if (!IsLoginRequest(context))
        {
            await _next(context);
            return;
        }

        var username = await TryReadUsernameAsync(context.Request);
        if (string.IsNullOrWhiteSpace(username))
        {
            await _next(context);
            return;
        }

        var status = lockoutService.Check(username);
        if (!status.Locked)
        {
            await _next(context);
            return;
        }

        _logger.LogWarning("Blocked login attempt for locked account: {Username}", username);

        var retryAfterSeconds = Math.Max(60, status.RemainingTimeMinutes * 60);
        context.Response.StatusCode = StatusCodes.Status423Locked;
        context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse
        {
            Success = false,
            Error = $"Account is temporarily locked. Try again in {status.RemainingTimeMinutes} minute(s).",
            Code = "ACCOUNT_LOCKED",
            Details = new
            {
                locked = true,
                remainingMinutes = status.RemainingTimeMinutes,
                retryAfter = retryAfterSeconds
            }
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    private bool IsLoginRequest(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return false;
        }

        return context.Request.Path.Equals(_loginPath, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> TryReadUsernameAsync(HttpRequest request)
    {
        if (request.ContentLength == null || request.ContentLength == 0)
        {
            return null;
        }

        request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
        }

        request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            // 尝试多种常见的用户名/邮箱字段名
            if (TryReadString(root, "username", out var username))
                return username;

            if (TryReadString(root, "userName", out var userName))
                return userName;

            if (TryReadString(root, "email", out var email))
                return email;
        }
        catch (JsonException)
        {
            // 忽略 JSON 解析错误
        }

        return null;
    }

    private static bool TryReadString(JsonElement root, string propertyName, out string? value)
    {
        value = null;
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return false;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
