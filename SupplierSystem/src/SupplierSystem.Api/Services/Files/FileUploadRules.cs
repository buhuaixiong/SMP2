using System.Globalization;
using System.Security.Cryptography;

namespace SupplierSystem.Api.Services.Files;

public static class FileUploadRules
{
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx",
        ".xlsx",
        ".xls",
        ".jpg",
        ".jpeg",
        ".png"
    };

    public static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel",
        "image/jpeg",
        "image/png"
    };

    public static string SanitizeFileName(string? name, string fallback = "upload")
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? fallback : name;
        Span<char> buffer = stackalloc char[baseName.Length];
        var length = 0;

        foreach (var ch in baseName)
        {
            var isAsciiLetter = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
            var isAsciiDigit = ch >= '0' && ch <= '9';
            if (isAsciiLetter || isAsciiDigit || ch == '_' || ch == '-' || ch == '.')
            {
                buffer[length++] = ch;
            }
            else
            {
                buffer[length++] = '_';
            }
        }

        return new string(buffer[..length]);
    }

    public static string BuildStoredName(string originalName)
    {
        var safeName = SanitizeFileName(originalName);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"{timestamp.ToString(CultureInfo.InvariantCulture)}-{safeName}";
    }

    public static string BuildRandomStoredName(string originalName)
    {
        var safeName = SanitizeFileName(originalName);
        Span<byte> random = stackalloc byte[4];
        RandomNumberGenerator.Fill(random);
        var suffix = BitConverter.ToUInt32(random);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"{timestamp}-{suffix}-{safeName}";
    }
}
