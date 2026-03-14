using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

public sealed partial class SettlementsController
{
    private async Task LogAuditAsync(AuthUser? actor, string action, string entityId, object payload)
    {
        if (actor == null)
        {
            return;
        }

        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "settlement",
                EntityId = entityId,
                Action = action,
                Changes = JsonSerializer.Serialize(payload),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Settlements] Failed to write audit entry.");
        }
    }

    private static IActionResult? RequireRole(AuthUser? user, params string[] allowedRoles)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var role = ControllerHelpers.NormalizeRole(user.Role);
        if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (allowedRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool TryReadIntFromQuery(IQueryCollection query, out int? value, params string[] keys)
    {
        value = null;
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var values))
            {
                if (int.TryParse(values.ToString(), out var parsed))
                {
                    value = parsed;
                    return true;
                }

                return false;
            }
        }

        return true;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                {
                    return numeric;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
                {
                    return numeric;
                }
            }
        }

        return null;
    }

    private static List<int> ReadIntArray(JsonElement body, params string[] keys)
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

    private static bool? ReadBool(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric != 0;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (bool.TryParse(raw, out var parsed))
                {
                    return parsed;
                }

                if (string.Equals(raw, "approved", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(raw, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (int.TryParse(raw, out numeric))
                {
                    return numeric != 0;
                }
            }
        }

        return null;
    }

    private static string? ReadRawJson(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            return value.GetRawText();
        }

        return null;
    }
}
