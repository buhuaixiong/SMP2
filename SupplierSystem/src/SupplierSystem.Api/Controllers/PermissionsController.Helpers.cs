using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class PermissionsController
{
    private IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (permissions.Length == 0)
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.All(granted.Contains))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        return null;
    }

    private static bool HasAnyPermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return false;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private static string? NormalizeRole(string? role)
    {
        return RolePermissions.GetRoleKey(role);
    }

    private static bool IsSupplierRole(string? role)
    {
        var key = RolePermissions.GetRoleKey(role);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return string.Equals(key, "temp_supplier", StringComparison.OrdinalIgnoreCase)
               || string.Equals(key, "formal_supplier", StringComparison.OrdinalIgnoreCase)
               || string.Equals(key, "supplier", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRoleLabel(string role)
    {
        return role;
    }

    private static object BuildUserResponse(User user)
    {
        return new
        {
            user.Id,
            user.Name,
            user.Username,
            user.Role,
            user.SupplierId,
            user.Email,
            user.Status,
            user.LastLoginAt,
            user.CreatedAt,
            user.UpdatedAt,
        };
    }

    private static object BuildUserResponseWithPermissions(User user)
    {
        return new
        {
            user.Id,
            user.Name,
            user.Username,
            user.Role,
            user.SupplierId,
            user.Email,
            user.Status,
            user.LastLoginAt,
            user.CreatedAt,
            user.UpdatedAt,
            permissions = RolePermissions.GetPermissionsForRole(user.Role),
        };
    }

    private static string? GetString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static int? GetInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    private static bool? GetBool(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                return value.GetBoolean();
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static List<int> GetIntArray(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var results = new List<int>();
            foreach (var entry in value.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var numeric))
                {
                    results.Add(numeric);
                    continue;
                }

                if (entry.ValueKind == JsonValueKind.String && int.TryParse(entry.GetString(), out numeric))
                {
                    results.Add(numeric);
                }
            }

            return results;
        }

        return new List<int>();
    }

    private async Task LogAuditAsync(string entityType, string? entityId, string action, object? changes, AuthUser user)
    {
        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = user.Id,
                ActorName = user.Name,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Changes = changes == null ? null : JsonSerializer.Serialize(changes),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Permissions] Failed to write audit entry.");
        }
    }
}
