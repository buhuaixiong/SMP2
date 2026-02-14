using System.Globalization;

namespace SupplierSystem.Api.Controllers;

public static class ControllerHelpers
{
    public static string? NormalizeRole(string? role)
    {
        return string.IsNullOrWhiteSpace(role) ? null : role.Trim().ToLowerInvariant();
    }

    public static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return parsed;
        }

        return null;
    }
}
