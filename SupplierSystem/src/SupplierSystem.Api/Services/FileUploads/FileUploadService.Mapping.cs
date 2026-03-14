using System.Reflection;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.FileUploads;

public sealed partial class FileUploadService
{
    private static Dictionary<string, object?> ToPayload(FileUploadSnapshot snapshot)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = snapshot.Id,
            ["supplierId"] = snapshot.SupplierId,
            ["fileId"] = snapshot.FileId,
            ["fileName"] = snapshot.FileName,
            ["fileDescription"] = snapshot.FileDescription,
            ["status"] = snapshot.Status,
            ["currentStep"] = snapshot.CurrentStep,
            ["submittedBy"] = snapshot.SubmittedBy,
            ["submittedAt"] = snapshot.SubmittedAt,
            ["riskLevel"] = snapshot.RiskLevel,
            ["validFrom"] = snapshot.ValidFrom,
            ["validTo"] = snapshot.ValidTo,
            ["createdAt"] = snapshot.CreatedAt,
            ["updatedAt"] = snapshot.UpdatedAt,
        };
    }

    private static List<Dictionary<string, object?>> ToWorkflowPayload()
    {
        return ApprovalWorkflow.Select(step => new Dictionary<string, object?>
        {
            ["step"] = step.Step,
            ["label"] = step.Label,
            ["permission"] = step.Permission,
            ["roles"] = step.Roles,
        }).ToList();
    }

    private static Dictionary<string, object?> MapApproval(Dictionary<string, object?> row)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = GetInt(row, "id"),
            ["uploadId"] = GetInt(row, "uploadId"),
            ["step"] = GetString(row, "step"),
            ["stepName"] = GetString(row, "stepName"),
            ["approverId"] = GetString(row, "approverId"),
            ["approverName"] = GetString(row, "approverName"),
            ["decision"] = GetString(row, "decision"),
            ["comments"] = GetString(row, "comments"),
            ["createdAt"] = GetDateString(row, "createdAt"),
        };
    }

    private static FileUploadSnapshot MapSnapshot(Dictionary<string, object?> row)
    {
        return new FileUploadSnapshot(
            GetInt(row, "id") ?? 0,
            GetInt(row, "supplierId") ?? GetInt(row, "supplier_id") ?? 0,
            GetInt(row, "fileId") ?? 0,
            GetString(row, "fileName"),
            GetString(row, "fileDescription"),
            GetString(row, "status") ?? string.Empty,
            GetString(row, "currentStep") ?? string.Empty,
            GetString(row, "submittedBy"),
            GetDateString(row, "submittedAt"),
            GetString(row, "riskLevel"),
            GetDateString(row, "validFrom"),
            GetDateString(row, "validTo"),
            GetDateString(row, "createdAt"),
            GetDateString(row, "updatedAt"));
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
