using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemLockdownController : NodeControllerBase
{
    private readonly SystemLockdownService _lockdownService;

    public SystemLockdownController(SystemLockdownService lockdownService, IWebHostEnvironment environment) : base(environment)
    {
        _lockdownService = lockdownService;
    }

    [HttpGet("lockdown")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _lockdownService.GetStatusAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                data = new
                {
                    isActive = status.IsActive,
                    announcement = status.Announcement,
                    activatedAt = status.ActivatedAt,
                }
            });
        }
        catch
        {
            return StatusCode(500, new { success = false, message = "Failed to get lockdown status" });
        }
    }

    [HttpGet("lockdown/full")]
    [Authorize]
    public async Task<IActionResult> GetFullStatus(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var status = await _lockdownService.GetStatusAsync(cancellationToken);
            return Ok(new { success = true, data = status });
        }
        catch
        {
            return StatusCode(500, new { success = false, message = "Failed to get lockdown status" });
        }
    }

    [HttpPost("lockdown/activate")]
    [Authorize]
    public async Task<IActionResult> Activate([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var reason = ReadString(body, "reason");
        var announcement = ReadString(body, "announcement");

        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new { success = false, message = "Reason is required and must be a non-empty string" });
        }

        if (string.IsNullOrWhiteSpace(announcement))
        {
            return BadRequest(new { success = false, message = "Announcement is required and must be a non-empty string" });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var result = await _lockdownService.ActivateAsync(
                user.Id,
                user.Name,
                reason.Trim(),
                announcement.Trim(),
                cancellationToken);

            return Ok(new { success = true, message = "Emergency lockdown activated successfully", data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Failed to activate lockdown", error = ex.Message });
        }
    }

    [HttpPost("lockdown/deactivate")]
    [Authorize]
    public async Task<IActionResult> Deactivate(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        try
        {
            var result = await _lockdownService.DeactivateAsync(user.Id, user.Name, cancellationToken);
            return Ok(new { success = true, message = "Emergency lockdown deactivated successfully", data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Failed to deactivate lockdown", error = ex.Message });
        }
    }

    [HttpGet("lockdown/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var limit = ParseInt(Request.Query["limit"], 50);
        limit = Math.Min(200, Math.Max(1, limit));

        try
        {
            var history = await _lockdownService.GetHistoryAsync(limit, cancellationToken);
            return Ok(new { success = true, data = history });
        }
        catch
        {
            return StatusCode(500, new { success = false, message = "Failed to get lockdown history" });
        }
    }

    private static IActionResult? RequireAdmin(AuthUser? user)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!granted.Contains(Permissions.AdminEmergencyLockdown))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
