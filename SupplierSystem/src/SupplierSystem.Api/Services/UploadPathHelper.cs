using System.Globalization;

namespace SupplierSystem.Api.Services;

public static class UploadPathHelper
{
    private static string GetUploadsRoot(IWebHostEnvironment environment)
    {
        var configured = Environment.GetEnvironmentVariable("UPLOADS_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Environment.GetEnvironmentVariable("UPLOAD_DIR");
        }

        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "uploads")
            : configured;

        Directory.CreateDirectory(root);
        return root;
    }

    public static string GetDocumentsRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "documents");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetInvoicesRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "invoices");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetSupplierFilesRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "suppliers");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetContractsRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "contracts");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetReconciliationRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "reconciliation");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetTempSupplierDocumentsRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "supplier-documents");
        Directory.CreateDirectory(path);
        return path;
    }

    // Returns uploads/supplier-documents/{safe-company-name-or-supplier-id}/
    public static string GetSupplierDocumentsRootByName(IWebHostEnvironment environment, int supplierId, string companyName)
    {
        var root = GetTempSupplierDocumentsRoot(environment);
        var safeFolderName = SanitizeFolderName(companyName, supplierId.ToString(CultureInfo.InvariantCulture));
        var path = Path.Combine(root, safeFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    // Keep Chinese/English letters, digits, space, hyphen, underscore where possible.
    private static string SanitizeFolderName(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var invalidChars = Path.GetInvalidFileNameChars()
            .Where(c => c != ' ' && c != '-' && c != '_');
        var sanitized = invalidChars.Aggregate(name, (current, c) => current.Replace(c, '_'));

        sanitized = sanitized.Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return fallback;
        }

        const int maxLength = 100;
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength);
        }

        return sanitized;
    }

    public static string GetRequisitionsRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "requisitions");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetRfqAttachmentsRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "rfq-attachments");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetRfqAttachmentsRoot(IWebHostEnvironment environment, long rfqId)
    {
        var root = GetRfqAttachmentsRoot(environment);
        if (rfqId <= 0)
        {
            return root;
        }

        var path = Path.Combine(root, rfqId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetTemplatesRoot(IWebHostEnvironment environment)
    {
        var path = Path.Combine(GetUploadsRoot(environment), "templates");
        Directory.CreateDirectory(path);
        return path;
    }

    public static string GetGenericUploadsRoot(IWebHostEnvironment environment)
    {
        return GetUploadsRoot(environment);
    }
}
