using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Services.Compliance;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[NodeResponse]
[Authorize]
[Route("api/whitelist")]
public sealed class WhitelistController : ControllerBase
{
    private static readonly HashSet<string> ExemptableDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "quality_assurance_agreement",
        "quality_compensation_agreement",
        "quality_kpi_targets",
        "incoming_packaging_transport_agreement"
    };

    private static readonly HashSet<string> NonExemptableDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "business_license",
        "tax_certificate",
        "bank_information"
    };

    private readonly WhitelistBlacklistStore _store;
    private readonly IAuditService _auditService;
    private readonly ILogger<WhitelistController> _logger;

    public WhitelistController(
        WhitelistBlacklistStore store,
        IAuditService auditService,
        ILogger<WhitelistController> logger)
    {
        _store = store;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("suppliers/search")]
    public async Task<IActionResult> SearchSuppliers([FromQuery] string? q, [FromQuery] string? stage, [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorWhitelistManage, Permissions.ProcurementDirectorBlacklistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var limitValue = Math.Min(200, Math.Max(1, limit ?? 20));
        var offsetValue = Math.Max(0, offset ?? 0);

        var query = _store.QuerySuppliers();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(s => s.CompanyName.Contains(q) || s.CompanyId.Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(s => s.Stage == stage);
        }

        var total = await query.CountAsync(cancellationToken);
        var suppliers = await query
            .OrderBy(s => s.CompanyName)
            .Skip(offsetValue)
            .Take(limitValue)
            .Select(s => new
            {
                s.Id,
                companyId = s.CompanyId,
                companyName = s.CompanyName,
                stage = s.Stage,
                category = s.Category,
                contactPerson = s.ContactPerson
            })
            .ToListAsync(cancellationToken);

        return Ok(new { suppliers, total });
    }

    [HttpGet("exemptions")]
    public async Task<IActionResult> ListExemptions(
        [FromQuery(Name = "supplier_id")] int? supplierId,
        [FromQuery(Name = "document_type")] string? documentType,
        [FromQuery(Name = "is_active")] string? isActive,
        CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorWhitelistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var query = _store.QueryExemptions();
        if (supplierId.HasValue)
        {
            query = query.Where(row => row.Exemption.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(row => row.Exemption.DocumentType == documentType);
        }

        if (TryParseBool(isActive, out var active))
        {
            query = query.Where(row => row.Exemption.IsActive == active);
        }

        var exemptions = await query
            .OrderByDescending(row => row.Exemption.ExemptedAt)
            .Select(row => new
            {
                id = row.Exemption.Id,
                supplier_id = row.Exemption.SupplierId,
                supplier_name = row.SupplierName,
                supplier_code = row.SupplierCode,
                supplier_stage = row.SupplierStage,
                document_type = row.Exemption.DocumentType,
                exempted_by = row.Exemption.ExemptedBy,
                exempted_by_name = row.Exemption.ExemptedByName,
                exempted_at = row.Exemption.ExemptedAt,
                reason = row.Exemption.Reason,
                expires_at = row.Exemption.ExpiresAt,
                is_active = row.Exemption.IsActive ? 1 : 0
            })
            .ToListAsync(cancellationToken);

        return Ok(new { exemptions });
    }

    [HttpPost("exemptions")]
    public async Task<IActionResult> CreateExemption([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorWhitelistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var supplierId = ReadInt(body, "supplier_id", "supplierId");
        var documentType = ReadString(body, "document_type", "documentType");
        var reason = ReadString(body, "reason");
        var expiresAt = ReadString(body, "expires_at", "expiresAt");

        if (!supplierId.HasValue || supplierId.Value <= 0 || string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new { message = "supplier_id, document_type, and reason are required." });
        }

        var supplier = await _store.FindSupplierAsync(supplierId.Value, cancellationToken);
        if (supplier == null)
        {
            return NotFound(new { message = "Supplier not found." });
        }

        var normalizedType = documentType.Trim();
        if (NonExemptableDocumentTypes.Contains(normalizedType))
        {
            return BadRequest(new
            {
                message = "This document type cannot be exempted. Basic registration documents are mandatory and non-exemptable.",
                document_type = normalizedType,
                non_exemptable_types = NonExemptableDocumentTypes
            });
        }

        if (!ExemptableDocumentTypes.Contains(normalizedType))
        {
            return BadRequest(new
            {
                message = $"Invalid document_type. Must be one of: {string.Join(", ", ExemptableDocumentTypes)}",
                exemptable_types = ExemptableDocumentTypes,
                non_exemptable_types = NonExemptableDocumentTypes
            });
        }

        var actor = HttpContext.GetAuthUser();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var normalizedReason = reason.Trim();
        var normalizedExpiresAt = NormalizeString(expiresAt);

        await using var transaction = await _store.BeginTransactionAsync(cancellationToken);
        try
        {
            var exemption = await _store.FindExemptionBySupplierAndTypeAsync(supplierId.Value, normalizedType, cancellationToken);

            if (exemption == null)
            {
                exemption = new SupplierDocumentWhitelist
                {
                    SupplierId = supplierId.Value,
                    DocumentType = normalizedType,
                    ExemptedBy = actor?.Id ?? "system",
                    ExemptedByName = actor?.Name,
                    ExemptedAt = now,
                    Reason = normalizedReason,
                    ExpiresAt = normalizedExpiresAt,
                    IsActive = true
                };

                _store.AddExemption(exemption);
            }
            else
            {
                exemption.ExemptedBy = actor?.Id ?? "system";
                exemption.ExemptedByName = actor?.Name;
                exemption.ExemptedAt = now;
                exemption.Reason = normalizedReason;
                exemption.ExpiresAt = normalizedExpiresAt;
                exemption.IsActive = true;
            }

            await _store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync(new AuditEntry
            {
                ActorId = actor?.Id,
                ActorName = actor?.Name,
                EntityType = "supplier_document_whitelist",
                EntityId = exemption.Id.ToString(),
                Action = "create_exemption",
                Changes = JsonSerializer.Serialize(new
                {
                    supplier_id = supplierId.Value,
                    document_type = normalizedType,
                    reason = normalizedReason,
                    expires_at = normalizedExpiresAt
                })
            });

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Document exemption added successfully",
                exemption_id = exemption.Id
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to add whitelist exemption.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to add whitelist exemption" });
        }
    }

    [HttpDelete("exemptions/{id:int}")]
    public async Task<IActionResult> DeleteExemption(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorWhitelistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var exemption = await _store.FindExemptionAsync(id, cancellationToken);
        if (exemption == null)
        {
            return NotFound(new { message = "Exemption not found." });
        }

        _store.RemoveExemption(exemption);
        await _store.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = HttpContext.GetAuthUser()?.Id,
            ActorName = HttpContext.GetAuthUser()?.Name,
            EntityType = "supplier_document_whitelist",
            EntityId = id.ToString(),
            Action = "delete_exemption",
            Changes = JsonSerializer.Serialize(new { supplier_id = exemption.SupplierId, document_type = exemption.DocumentType })
        });

        return Ok(new { message = "Exemption deleted successfully" });
    }

    [HttpPatch("exemptions/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateExemption(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorWhitelistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var exemption = await _store.FindExemptionAsync(id, cancellationToken);
        if (exemption == null)
        {
            return NotFound(new { message = "Exemption not found." });
        }

        exemption.IsActive = false;
        await _store.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = HttpContext.GetAuthUser()?.Id,
            ActorName = HttpContext.GetAuthUser()?.Name,
            EntityType = "supplier_document_whitelist",
            EntityId = id.ToString(),
            Action = "deactivate_exemption",
            Changes = JsonSerializer.Serialize(new { supplier_id = exemption.SupplierId, document_type = exemption.DocumentType })
        });

        return Ok(new { message = "Exemption deactivated successfully" });
    }

    private static IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.Any(granted.Contains) && !string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (value == "1")
        {
            result = true;
            return true;
        }

        if (value == "0")
        {
            result = false;
            return true;
        }

        return false;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static string? NormalizeString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task LogAuditAsync(AuditEntry entry)
    {
        try
        {
            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write whitelist audit entry.");
        }
    }
}

[ApiController]
[NodeResponse]
[Authorize]
[Route("api/blacklist")]
public sealed class BlacklistController : ControllerBase
{
    private readonly WhitelistBlacklistStore _store;
    private readonly IAuditService _auditService;
    private readonly ILogger<BlacklistController> _logger;

    public BlacklistController(
        WhitelistBlacklistStore store,
        IAuditService auditService,
        ILogger<BlacklistController> logger)
    {
        _store = store;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet("entries")]
    public async Task<IActionResult> ListEntries(
        [FromQuery(Name = "blacklist_type")] string? blacklistType,
        [FromQuery(Name = "severity")] string? severity,
        [FromQuery(Name = "is_active")] string? isActive,
        CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorBlacklistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var query = _store.QueryBlacklistEntries();
        if (!string.IsNullOrWhiteSpace(blacklistType))
        {
            query = query.Where(entry => entry.BlacklistType == blacklistType);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(entry => entry.Severity == severity);
        }

        if (TryParseBool(isActive, out var active))
        {
            query = query.Where(entry => entry.IsActive == active);
        }

        var entries = await query
            .OrderByDescending(entry => entry.AddedAt)
            .Select(entry => new
            {
                id = entry.Id,
                blacklist_type = entry.BlacklistType,
                blacklist_value = entry.BlacklistValue,
                reason = entry.Reason,
                severity = entry.Severity,
                added_by = entry.AddedBy,
                added_by_name = entry.AddedByName,
                added_at = entry.AddedAt,
                expires_at = entry.ExpiresAt,
                is_active = entry.IsActive ? 1 : 0,
                notes = entry.Notes
            })
            .ToListAsync(cancellationToken);

        return Ok(new { entries });
    }

    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorBlacklistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var type = ReadString(body, "blacklist_type", "blacklistType");
        var value = ReadString(body, "blacklist_value", "blacklistValue");
        var reason = ReadString(body, "reason");
        var severity = ReadString(body, "severity");
        var expiresAt = ReadString(body, "expires_at", "expiresAt");
        var notes = ReadString(body, "notes");

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest(new { message = "blacklist_type, blacklist_value, and reason are required." });
        }

        var normalizedType = type.Trim();
        if (normalizedType != "credit_code" && normalizedType != "email")
        {
            return BadRequest(new { message = "Invalid blacklist_type. Must be credit_code or email." });
        }

        var normalizedSeverity = string.IsNullOrWhiteSpace(severity) ? "high" : severity.Trim();
        if (normalizedSeverity != "critical" && normalizedSeverity != "high" && normalizedSeverity != "medium")
        {
            normalizedSeverity = "high";
        }

        var normalizedValue = value.Trim().ToLowerInvariant();
        var exists = await _store.BlacklistEntryExistsAsync(normalizedType, normalizedValue, cancellationToken);
        if (exists)
        {
            return Conflict(new { message = "This value is already blacklisted." });
        }

        var actor = HttpContext.GetAuthUser();
        var entryRecord = new SupplierRegistrationBlacklist
        {
            BlacklistType = normalizedType,
            BlacklistValue = normalizedValue,
            Reason = reason.Trim(),
            Severity = normalizedSeverity,
            AddedBy = actor?.Id ?? "system",
            AddedByName = actor?.Name,
            AddedAt = DateTimeOffset.UtcNow.ToString("o"),
            ExpiresAt = NormalizeString(expiresAt),
            IsActive = true,
            Notes = NormalizeString(notes)
        };

        _store.AddBlacklistEntry(entryRecord);
        await _store.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = actor?.Id,
            ActorName = actor?.Name,
            EntityType = "supplier_registration_blacklist",
            EntityId = entryRecord.Id.ToString(),
            Action = "create_blacklist",
            Changes = JsonSerializer.Serialize(new
            {
                blacklist_type = normalizedType,
                blacklist_value = normalizedValue,
                reason = entryRecord.Reason,
                severity = normalizedSeverity
            })
        });

        return StatusCode(StatusCodes.Status201Created, new { message = "Blacklist entry added successfully", entry_id = entryRecord.Id });
    }

    [HttpDelete("entries/{id:int}")]
    public async Task<IActionResult> DeleteEntry(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorBlacklistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var entry = await _store.FindBlacklistEntryAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound(new { message = "Blacklist entry not found." });
        }

        _store.RemoveBlacklistEntry(entry);
        await _store.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = HttpContext.GetAuthUser()?.Id,
            ActorName = HttpContext.GetAuthUser()?.Name,
            EntityType = "supplier_registration_blacklist",
            EntityId = id.ToString(),
            Action = "delete_blacklist",
            Changes = JsonSerializer.Serialize(new { blacklist_type = entry.BlacklistType, blacklist_value = entry.BlacklistValue })
        });

        return Ok(new { message = "Blacklist entry deleted successfully" });
    }

    [HttpPatch("entries/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateEntry(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.ProcurementDirectorBlacklistManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var entry = await _store.FindBlacklistEntryAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound(new { message = "Blacklist entry not found." });
        }

        entry.IsActive = false;
        await _store.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = HttpContext.GetAuthUser()?.Id,
            ActorName = HttpContext.GetAuthUser()?.Name,
            EntityType = "supplier_registration_blacklist",
            EntityId = id.ToString(),
            Action = "deactivate_blacklist",
            Changes = JsonSerializer.Serialize(new { blacklist_type = entry.BlacklistType, blacklist_value = entry.BlacklistValue })
        });

        return Ok(new { message = "Blacklist entry deactivated successfully" });
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateEntry([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var creditCode = ReadString(body, "credit_code", "creditCode");
        var email = ReadString(body, "email");

        if (string.IsNullOrWhiteSpace(creditCode) && string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Either credit_code or email is required." });
        }

        var now = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(creditCode))
        {
            var normalized = creditCode.Trim().ToLowerInvariant();
            var entry = await FindActiveEntryAsync("credit_code", normalized, now, cancellationToken);
            if (entry != null)
            {
                return Ok(new { is_blacklisted = true, reason = entry.Reason, severity = entry.Severity, blacklist_type = "credit_code" });
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLowerInvariant();
            var entry = await FindActiveEntryAsync("email", normalized, now, cancellationToken);
            if (entry != null)
            {
                return Ok(new { is_blacklisted = true, reason = entry.Reason, severity = entry.Severity, blacklist_type = "email" });
            }
        }

        return Ok(new { is_blacklisted = false, reason = (string?)null, severity = (string?)null });
    }

    private async Task<SupplierRegistrationBlacklist?> FindActiveEntryAsync(string type, string value, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = await _store.FindActiveBlacklistEntryAsync(type, value, cancellationToken);
        if (entry == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.ExpiresAt))
        {
            return entry;
        }

        if (DateTimeOffset.TryParse(entry.ExpiresAt, out var expiresAt))
        {
            return expiresAt > now ? entry : null;
        }

        return entry;
    }

    private static IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.Any(granted.Contains) && !string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (value == "1")
        {
            result = true;
            return true;
        }

        if (value == "0")
        {
            result = false;
            return true;
        }

        return false;
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static string? NormalizeString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task LogAuditAsync(AuditEntry entry)
    {
        try
        {
            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write blacklist audit entry.");
        }
    }
}
