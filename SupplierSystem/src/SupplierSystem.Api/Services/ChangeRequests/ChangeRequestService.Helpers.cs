using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.ChangeRequests;

public sealed partial class ChangeRequestService
{
    private async Task ApplyApprovedChangesAsync(ChangeRequestSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Payload.Count == 0)
        {
            throw new ChangeRequestServiceException(400, "Invalid change payload");
        }

        var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(
            item => item.Id == snapshot.SupplierId,
            cancellationToken);
        if (supplier == null)
        {
            throw new ChangeRequestServiceException(400, "Supplier not found");
        }

        var sanitizedChanges = SanitizeChangesPayload(snapshot.Payload);
        foreach (var entry in sanitizedChanges)
        {
            var property = typeof(Supplier).GetProperty(
                entry.Key,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (property == null || !property.CanWrite)
            {
                continue;
            }

            var value = entry.Value;
            if (property.PropertyType == typeof(string))
            {
                property.SetValue(supplier, value);
                continue;
            }

            if (value == null)
            {
                property.SetValue(supplier, null);
                continue;
            }

            try
            {
                var converted = Convert.ChangeType(value, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
                property.SetValue(supplier, converted);
            }
            catch
            {
                // Ignore conversion issues to mimic Node's permissive updates.
            }
        }

        supplier.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool CanAccessRequest(AuthUser user, int supplierId)
    {
        var isSelf = user.SupplierId.HasValue && user.SupplierId.Value == supplierId;
        var isStaff = !string.IsNullOrWhiteSpace(user.Role) &&
                      !string.Equals(user.Role, "temp_supplier", StringComparison.OrdinalIgnoreCase) &&
                      !string.Equals(user.Role, "formal_supplier", StringComparison.OrdinalIgnoreCase);
        return isSelf || isStaff;
    }

    private static IReadOnlyList<ChangeRequestWorkflowStep> GetWorkflowForChangeType(string changeType)
    {
        if (LegacyRequiredTypes.Contains(changeType))
        {
            return RequiredWorkflow;
        }

        if (string.Equals(changeType, ChangeTypeOptional, StringComparison.OrdinalIgnoreCase))
        {
            return OptionalWorkflow;
        }

        return RequiredWorkflow;
    }

    private static string GetFlowForChangeType(string changeType)
    {
        if (LegacyRequiredTypes.Contains(changeType))
        {
            return "required";
        }

        if (string.Equals(changeType, ChangeTypeOptional, StringComparison.OrdinalIgnoreCase))
        {
            return "optional";
        }

        return "required";
    }

    private static ChangeRequestWorkflowStep? GetNextStep(
        IReadOnlyList<ChangeRequestWorkflowStep> workflow,
        string currentStep)
    {
        var index = workflow.ToList().FindIndex(step =>
            string.Equals(step.Step, currentStep, StringComparison.OrdinalIgnoreCase));
        if (index == -1 || index >= workflow.Count - 1)
        {
            return null;
        }

        return workflow[index + 1];
    }

    private static bool HasApprovalPermission(AuthUser user, ChangeRequestWorkflowStep step)
    {
        var permissions = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (permissions.Contains(step.Permission))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(user.Role))
        {
            return false;
        }

        return step.Roles.Any(role => string.Equals(role, user.Role, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> CollectUserApprovalSteps(AuthUser user)
    {
        var permissions = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var steps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var workflow in AllWorkflows)
        {
            foreach (var step in workflow)
            {
                if (permissions.Contains(step.Permission) ||
                    (!string.IsNullOrWhiteSpace(user.Role) &&
                     step.Roles.Any(role => string.Equals(role, user.Role, StringComparison.OrdinalIgnoreCase))))
                {
                    steps.Add(step.Step);
                }
            }
        }

        return steps.ToList();
    }

    private static (List<ChangeRequestField> required, List<ChangeRequestField> optional) BuildChangedFieldSummary(
        IReadOnlyDictionary<string, string?> sanitizedChanges)
    {
        var required = new List<ChangeRequestField>();
        var optional = new List<ChangeRequestField>();

        foreach (var entry in sanitizedChanges)
        {
            var label = RequiredFieldLabels.TryGetValue(entry.Key, out var requiredLabel)
                ? requiredLabel
                : OptionalFieldLabels.TryGetValue(entry.Key, out var optionalLabel)
                    ? optionalLabel
                    : entry.Key;

            var field = new ChangeRequestField(entry.Key, label, entry.Value);
            if (RequiredFieldKeys.Contains(entry.Key))
            {
                required.Add(field);
            }
            else
            {
                optional.Add(field);
            }
        }

        return (required, optional);
    }

    private static string CalculateRiskLevel(IEnumerable<ChangeRequestField> changedFields)
    {
        if (changedFields.Any(field => HighRiskFields.Contains(field.Key)))
        {
            return "high";
        }

        var count = changedFields.Count();
        if (count >= 5)
        {
            return "medium";
        }

        return "low";
    }

    private static Dictionary<string, string?> SanitizeChangesPayload(IDictionary<string, object?> changes)
    {
        var sanitized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in changes)
        {
            if (entry.Value == null)
            {
                sanitized[entry.Key] = null;
                continue;
            }

            var rawValue = ToStringValue(entry.Value);
            sanitized[entry.Key] = rawValue == null ? null : Sanitize(rawValue);
        }

        return sanitized;
    }

    private static Dictionary<string, string?> SanitizeChangesPayload(IReadOnlyDictionary<string, string?> changes)
    {
        var sanitized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in changes)
        {
            if (entry.Value == null)
            {
                sanitized[entry.Key] = null;
                continue;
            }

            sanitized[entry.Key] = Sanitize(entry.Value);
        }

        return sanitized;
    }

    private static Dictionary<string, string?> ParsePayloadMap(object? raw)
    {
        if (raw == null)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        if (raw is DBNull)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        if (raw is Dictionary<string, string?> map)
        {
            return new Dictionary<string, string?>(map, StringComparer.OrdinalIgnoreCase);
        }

        if (raw is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var mapFromElement = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    mapFromElement[property.Name] = ToStringValue(property.Value);
                }
                return mapFromElement;
            }
        }

        if (raw is string json)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, PayloadSerializerOptions);
                if (parsed != null)
                {
                    return new Dictionary<string, string?>(parsed, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Ignore parse errors.
            }
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ToStringValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                JsonValueKind.Array => string.Join(",", element.EnumerateArray()
                    .Select(item => ToStringValue(item) ?? string.Empty)),
                JsonValueKind.Object => element.GetRawText(),
                _ => element.ToString()
            };
        }

        return value.ToString();
    }

    private static string Sanitize(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
