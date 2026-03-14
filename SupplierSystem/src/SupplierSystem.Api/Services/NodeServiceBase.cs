using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Services;

public abstract class NodeServiceBase
{
    protected static void RequirePermissions(AuthUser? user, IEnumerable<string> permissions)
    {
        if (user == null)
        {
            throw new ServiceErrorException(401, "Authentication required", "AUTH_REQUIRED");
        }

        var required = permissions?.ToList() ?? new List<string>();
        if (required.Count == 0)
        {
            return;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!required.All(granted.Contains))
        {
            throw new ServiceErrorException(
                403,
                $"Permission denied. Required: {string.Join(" & ", required)}",
                "PERMISSION_DENIED");
        }
    }

    protected static void RequireAnyPermission(AuthUser? user, IEnumerable<string> permissions)
    {
        if (user == null)
        {
            throw new ServiceErrorException(401, "Authentication required", "AUTH_REQUIRED");
        }

        var required = permissions?.ToList() ?? new List<string>();
        if (required.Count == 0)
        {
            return;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!required.Any(granted.Contains))
        {
            throw new ServiceErrorException(
                403,
                $"Permission denied. Required any of: {string.Join(", ", required)}",
                "PERMISSION_DENIED");
        }
    }

    protected static bool HasAnyPermission(AuthUser? user, IEnumerable<string> permissions)
    {
        if (user == null)
        {
            return false;
        }

        var required = permissions?.ToList() ?? new List<string>();
        if (required.Count == 0)
        {
            return true;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return required.Any(granted.Contains);
    }

    protected static bool IsSupplierSelf(AuthUser? user, int? supplierId)
    {
        if (user == null || !user.SupplierId.HasValue || !supplierId.HasValue)
        {
            return false;
        }

        return user.SupplierId.Value == supplierId.Value;
    }

    protected static void ValidateRequired(IDictionary<string, object?>? data, IEnumerable<string> requiredFields)
    {
        if (data == null)
        {
            throw new ValidationErrorException("Invalid data object for validation");
        }

        var missing = new List<string>();
        foreach (var field in requiredFields)
        {
            if (!data.TryGetValue(field, out var value) || value == null)
            {
                missing.Add(field);
                continue;
            }

            if (value is string text && string.IsNullOrWhiteSpace(text))
            {
                missing.Add(field);
            }
        }

        if (missing.Count > 0)
        {
            throw new ValidationErrorException(
                $"Missing required fields: {string.Join(", ", missing)}",
                new { missingFields = missing });
        }
    }

    protected static string? SanitizeString(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
