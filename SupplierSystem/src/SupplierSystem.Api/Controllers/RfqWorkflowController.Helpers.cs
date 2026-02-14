using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    private IActionResult? RequireAnyPermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (permissions.Length == 0)
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.Any(granted.Contains))
        {
            return StatusCode(403, new { message = "Access denied for current role." });
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string? GetSearchKeyword(IQueryCollection query)
    {
        var candidates = new[] { query["search"].ToString(), query["keyword"].ToString(), query["q"].ToString() };
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    private string ResolveUploadDirectory(long? rfqId = null)
    {
        return rfqId.HasValue
            ? UploadPathHelper.GetRfqAttachmentsRoot(_environment, rfqId.Value)
            : UploadPathHelper.GetRfqAttachmentsRoot(_environment);
    }

    private static string DecodeFileName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name ?? string.Empty;
        }

        try
        {
            var bytes = Encoding.Latin1.GetBytes(name);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return name;
        }
    }

    private static decimal? ReadDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : decimal.TryParse(value, out parsed)
                ? parsed
                : null;
    }

    private static object? ParseJsonValue(string? json, object? fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return fallback;
        }
    }

    private async Task LogAuditAsync(
        string entityType,
        string entityId,
        string action,
        object? changes,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = new SupplierSystem.Application.Models.Audit.AuditEntry
            {
                ActorId = user.Id,
                ActorName = user.Name,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Changes = changes == null ? null : JsonSerializer.Serialize(changes),
            };

            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry for {EntityType} {EntityId}", entityType, entityId);
        }
    }

    private sealed record StoredFile(
        string OriginalName,
        string StoredName,
        string FilePath,
        string? ContentType,
        long Size);
}
