using System.Globalization;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Services.FileUploads;

public sealed partial class FileUploadService
{
    private static FileUploadWorkflowStep? GetNextStep(string currentStepKey)
    {
        if (!WorkflowStepIndex.TryGetValue(currentStepKey, out var current))
        {
            return null;
        }

        var nextOrder = current.Order + 1;
        return ApprovalWorkflow.FirstOrDefault(step => step.Order == nextOrder);
    }

    private static bool HasStepPermission(AuthUser user, FileUploadWorkflowStep step)
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

    private static (DateTimeOffset validFrom, DateTimeOffset validTo) ValidateValidityPeriod(
        string? validFrom,
        string? validTo)
    {
        if (string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(validTo))
        {
            throw new FileUploadServiceException(400, "File validity period is required (validFrom and validTo)");
        }

        if (!DateTimeOffset.TryParse(validFrom, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var fromDate))
        {
            throw new FileUploadServiceException(400, "Invalid validFrom date format");
        }

        if (!DateTimeOffset.TryParse(validTo, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var toDate))
        {
            throw new FileUploadServiceException(400, "Invalid validTo date format");
        }

        var today = DateTimeOffset.Now.Date;
        if (fromDate.Date < today)
        {
            throw new FileUploadServiceException(400, "validFrom date cannot be in the past");
        }

        if (toDate.Date <= fromDate.Date)
        {
            throw new FileUploadServiceException(400, "validTo date must be after validFrom date");
        }

        return (fromDate, toDate);
    }

    private static bool CanAccessUpload(AuthUser user, int supplierId)
    {
        var isSelf = user.SupplierId.HasValue && user.SupplierId.Value == supplierId;
        var isStaff = !string.IsNullOrWhiteSpace(user.Role) &&
                      !string.Equals(user.Role, "temp_supplier", StringComparison.OrdinalIgnoreCase) &&
                      !string.Equals(user.Role, "formal_supplier", StringComparison.OrdinalIgnoreCase);
        return isSelf || isStaff;
    }

    private static List<string> CollectUserApprovalSteps(AuthUser user)
    {
        var permissions = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var steps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in ApprovalWorkflow)
        {
            if (permissions.Contains(step.Permission) ||
                (!string.IsNullOrWhiteSpace(user.Role) &&
                 step.Roles.Any(role => string.Equals(role, user.Role, StringComparison.OrdinalIgnoreCase))))
            {
                steps.Add(step.Step);
            }
        }

        return steps.ToList();
    }

    private static bool IsPurchaser(AuthUser user)
    {
        return string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
