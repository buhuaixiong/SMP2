using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RequisitionsController
{
    private static readonly HashSet<string> ProcurementRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "purchaser",
        "procurement_manager",
        "procurement_director",
        "admin",
    };

    private static readonly HashSet<string> ValidItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "equipment",
        "consumables",
        "fixtures",
        "molds",
        "blades",
        "hardware",
    };

    private static readonly HashSet<string> ValidEquipmentSubtypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "standard",
        "non-standard",
        "non_standard",
    };

    private static readonly HashSet<string> ValidConsumableSubtypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "labor_protection",
        "cleaning",
        "office",
        "accessories",
        "production",
        "other",
    };

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".ppt",
        ".pptx",
        ".jpg",
        ".jpeg",
        ".png",
        ".txt",
    };

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

    private static bool IsProcurementUser(AuthUser user)
    {
        return ProcurementRoles.Contains(user.Role ?? string.Empty);
    }

    private static bool IsDepartmentUser(AuthUser user)
    {
        return string.Equals(user.Role, "department_user", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AuthUser?> EnsureDepartmentAsync(AuthUser user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.Department))
        {
            return user;
        }

        var dbUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        if (dbUser == null || string.IsNullOrWhiteSpace(dbUser.Department))
        {
            return null;
        }

        user.Department = dbUser.Department;
        _cache.Set($"auth:user:{user.Id}", user, TimeSpan.FromMinutes(5));
        _schemaMonitor.RecordRepair(user.Id, "requisitions-get");
        return user;
    }

    private static void ValidateItemRequirements(IReadOnlyList<Models.Requisitions.RequisitionItemInput> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.ItemName) || item.Quantity <= 0)
            {
                throw new InvalidOperationException("Each item must have a name and positive quantity.");
            }

            if (string.IsNullOrWhiteSpace(item.ItemType) || !ValidItemTypes.Contains(item.ItemType))
            {
                throw new InvalidOperationException("Each item must have a valid item type.");
            }

            if (string.Equals(item.ItemType, "equipment", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(item.ItemSubtype) || !ValidEquipmentSubtypes.Contains(item.ItemSubtype)))
            {
                throw new InvalidOperationException("Equipment items must have subtype: standard or non-standard.");
            }

            if (string.Equals(item.ItemType, "consumables", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(item.ItemSubtype) || !ValidConsumableSubtypes.Contains(item.ItemSubtype)))
            {
                throw new InvalidOperationException("Consumables items must have a valid subtype.");
            }
        }
    }

    private static string GenerateRequisitionNumber()
    {
        var now = DateTimeOffset.UtcNow;
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        Span<byte> random = stackalloc byte[3];
        RandomNumberGenerator.Fill(random);
        var suffix = Convert.ToHexString(random);
        return $"REQ-{date}-{suffix}";
    }

    private string ResolveUploadDirectory()
    {
        return UploadPathHelper.GetRequisitionsRoot(_environment);
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

    private static bool IsAllowedAttachment(IFormFile file)
    {
        if (file == null)
        {
            return false;
        }

        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        return AllowedAttachmentExtensions.Contains(extension);
    }

    private async Task LogAuditAsync(
        string entityType,
        string? entityId,
        string action,
        object? changes,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = new AuditEntry
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
            _logger.LogWarning(ex, "[Requisitions] Failed to write audit entry.");
        }
    }
}
