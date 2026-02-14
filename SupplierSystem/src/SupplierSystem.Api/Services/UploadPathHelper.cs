using System.Globalization;

namespace SupplierSystem.Api.Services;

public static class UploadPathHelper
{
    private static string GetUploadsRoot(IWebHostEnvironment environment)
    {
        var configured = Environment.GetEnvironmentVariable("UPLOADS_PATH");
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

    /// <summary>
    /// 获取按供应商名称分组的临时供应商文档路径
    /// </summary>
    /// <param name="environment">Web宿主环境</param>
    /// <param name="supplierId">供应商ID</param>
    /// <param name="companyName">供应商公司名称（用于文件夹命名）</param>
    /// <returns>路径: uploads/supplier-documents/{SafeCompanyName}/</returns>
    public static string GetSupplierDocumentsRootByName(IWebHostEnvironment environment, int supplierId, string companyName)
    {
        var root = GetTempSupplierDocumentsRoot(environment);
        // 安全的文件夹名称：移除非法字符
        var safeFolderName = SanitizeFolderName(companyName, supplierId.ToString());
        var path = Path.Combine(root, safeFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 安全的文件夹名称：移除非法字符，保留中文、英文、数字、空格、连字符、下划线
    /// </summary>
    private static string SanitizeFolderName(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        // 替换非法字符为下划线
        var invalidChars = Path.GetInvalidFileNameChars()
            .Where(c => c != ' ' && c != '-' && c != '_'); // 允许空格、连字符、下划线
        var sanitized = invalidChars.Aggregate(name, (current, c) => current.Replace(c, '_'));

        // 移除首尾空格和点
        sanitized = sanitized.Trim().Trim('.');

        // 如果清理后为空，使用fallback
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return fallback;
        }

        // 限制文件夹名称长度（Windows路径限制260字符）
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
