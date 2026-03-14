using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ContractsController
{
    private static bool HasAnyPermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return false;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private IActionResult? RequireContractAccess(AuthUser? user, bool allowSupplier)
    {
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (HasAnyPermission(user, StaffContractPermissions))
        {
            return null;
        }

        if (allowSupplier && user.SupplierId.HasValue && HasAnyPermission(user, SupplierContractPermissions))
        {
            return null;
        }

        return StatusCode(403, new { message = "Access denied." });
    }

    private static bool TryAssignString(JsonElement payload, string name, Action<string?> assign)
    {
        if (!payload.TryGetProperty(name, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            assign(null);
            return true;
        }

        assign(element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString());
        return true;
    }

    private static bool TryAssignDecimal(JsonElement payload, string name, Action<decimal?> assign)
    {
        if (!payload.TryGetProperty(name, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            assign(null);
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var value))
        {
            assign(value);
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), out value))
        {
            assign(value);
            return true;
        }

        return false;
    }

    private static bool TryAssignBool(JsonElement payload, string name, Action<bool> assign)
    {
        if (!payload.TryGetProperty(name, out var element))
        {
            return false;
        }

        var parsed = element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt32(out var numeric) && numeric != 0,
            JsonValueKind.String => TryParseBoolean(element.GetString(), out var value) && value,
            _ => false
        };

        if (element.ValueKind == JsonValueKind.String && !TryParseBoolean(element.GetString(), out _))
        {
            return false;
        }

        assign(parsed);
        return true;
    }

    private static bool TryParseBoolean(string? value, out bool parsed)
    {
        parsed = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out parsed))
        {
            return true;
        }

        if (int.TryParse(value, out var numeric))
        {
            parsed = numeric != 0;
            return true;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "yes" or "on")
        {
            parsed = true;
            return true;
        }

        if (normalized is "no" or "off")
        {
            parsed = false;
            return true;
        }

        return false;
    }

    private static bool TryParseDate(string? value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParse(value, out parsed);
    }

    private static DateTime? ResolveSortDate(string? value)
    {
        return TryParseDate(value, out var parsed) ? parsed : null;
    }

    private void DeleteContractFiles(IEnumerable<ContractVersion> versions)
    {
        var contractsRoot = UploadPathHelper.GetContractsRoot(_environment);
        foreach (var version in versions)
        {
            if (string.IsNullOrWhiteSpace(version.StoredName))
            {
                continue;
            }

            try
            {
                var filePath = Path.Combine(contractsRoot, version.StoredName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch
            {
                // Ignore file delete failures.
            }
        }
    }
}
