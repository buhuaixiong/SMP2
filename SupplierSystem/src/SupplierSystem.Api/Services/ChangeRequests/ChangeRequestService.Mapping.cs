using System.Linq;
using System.Reflection;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.ChangeRequests;

public sealed partial class ChangeRequestService
{
    private static List<Dictionary<string, object?>> ToFieldPayloads(IEnumerable<ChangeRequestField> fields, string category)
    {
        return fields.Select(field => new Dictionary<string, object?>
        {
            ["key"] = field.Key,
            ["label"] = field.Label,
            ["newValue"] = field.NewValue,
            ["category"] = category,
        }).ToList();
    }

    private static List<Dictionary<string, object?>> ToWorkflowPayload(IReadOnlyList<ChangeRequestWorkflowStep> workflow)
    {
        return workflow.Select(step => new Dictionary<string, object?>
        {
            ["step"] = step.Step,
            ["label"] = step.Label,
            ["permission"] = step.Permission,
            ["roles"] = step.Roles,
        }).ToList();
    }

    private static Dictionary<string, object?> ToPayload(ChangeRequestSnapshot snapshot)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = snapshot.Id,
            ["supplierId"] = snapshot.SupplierId,
            ["changeType"] = snapshot.ChangeType,
            ["status"] = snapshot.Status,
            ["currentStep"] = snapshot.CurrentStep,
            ["payload"] = snapshot.Payload,
            ["submittedBy"] = snapshot.SubmittedBy,
            ["submittedAt"] = snapshot.SubmittedAt,
            ["updatedAt"] = snapshot.UpdatedAt,
            ["riskLevel"] = snapshot.RiskLevel,
            ["requiresQuality"] = snapshot.RequiresQuality,
        };

        if (!string.IsNullOrWhiteSpace(snapshot.CompanyName))
        {
            payload["companyName"] = snapshot.CompanyName;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.CompanyId))
        {
            payload["companyId"] = snapshot.CompanyId;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.Stage))
        {
            payload["stage"] = snapshot.Stage;
        }

        return payload;
    }

    private static Dictionary<string, object?> MapApproval(Dictionary<string, object?> row)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = GetInt(row, "id"),
            ["requestId"] = GetInt(row, "requestId"),
            ["step"] = GetString(row, "step"),
            ["approverId"] = GetString(row, "approverId"),
            ["approverName"] = GetString(row, "approverName"),
            ["decision"] = GetString(row, "decision"),
            ["comments"] = GetString(row, "comments"),
            ["createdAt"] = GetDateString(row, "createdAt"),
        };
    }

    private static ChangeRequestSnapshot MapSnapshot(Dictionary<string, object?> row)
    {
        return new ChangeRequestSnapshot(
            GetInt(row, "id") ?? 0,
            GetInt(row, "supplierId") ?? GetInt(row, "supplier_id") ?? 0,
            GetString(row, "changeType") ?? string.Empty,
            GetString(row, "status") ?? string.Empty,
            GetString(row, "currentStep") ?? string.Empty,
            ParsePayloadMap(row.TryGetValue("payload", out var payload) ? payload : null),
            GetString(row, "submittedBy"),
            GetDateString(row, "submittedAt"),
            GetDateString(row, "updatedAt"),
            GetString(row, "riskLevel"),
            GetInt(row, "requiresQuality"),
            GetString(row, "companyName"),
            GetString(row, "companyId"),
            GetString(row, "stage"));
    }

    private static Dictionary<string, object?> MapSupplier(Supplier supplier)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in typeof(Supplier).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var name = property.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var camelName = char.ToLowerInvariant(name[0]) + name[1..];
            result[camelName] = property.GetValue(supplier);
        }

        return result;
    }

    private static DateTimeOffset GetSortTimestamp(Dictionary<string, object?> payload)
    {
        if (payload.TryGetValue("submittedAt", out var value))
        {
            var parsed = ParseDate(value);
            if (parsed.HasValue)
            {
                return parsed.Value;
            }
        }

        return DateTimeOffset.MinValue;
    }

    private static DateTimeOffset? ParseDate(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is DateTimeOffset dto)
        {
            return dto;
        }

        if (value is DateTime dt)
        {
            return new DateTimeOffset(dt.ToUniversalTime());
        }

        if (value is string text && DateTimeOffset.TryParse(text, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int? GetInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            bool boolValue => boolValue ? 1 : 0,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string? GetDateString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset dto)
        {
            return dto.ToString("o");
        }

        if (value is DateTime dt)
        {
            return new DateTimeOffset(dt.ToUniversalTime()).ToString("o");
        }

        return value.ToString();
    }
}
