using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/audit-archive")]
public sealed class AuditArchiveController : NodeControllerBase
{
    private readonly IAuditArchiveService _archiveService;
    private readonly IAuditService _auditService;
    private readonly SupplierSystemDbContext _dbContext;

    public AuditArchiveController(
        IAuditArchiveService archiveService,
        IAuditService auditService,
        SupplierSystemDbContext dbContext,
        IWebHostEnvironment environment) : base(environment)
    {
        _archiveService = archiveService;
        _auditService = auditService;
        _dbContext = dbContext;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var stats = await _archiveService.GetStatsAsync(cancellationToken);
        return Ok(new { data = stats });
    }

    [HttpPost("verify/{id:int}")]
    public async Task<IActionResult> VerifyLog(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid audit log ID." });
        }

        var result = await _archiveService.VerifyArchivedLogAsync(id, cancellationToken);
        return Ok(new { data = result });
    }

    [HttpPost("verify-chain")]
    public async Task<IActionResult> VerifyChain(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        int? startId = ParseIntNullable(Request.Query["startId"]);
        int? endId = ParseIntNullable(Request.Query["endId"]);

        if ((Request.Query.ContainsKey("startId") && !startId.HasValue) ||
            (Request.Query.ContainsKey("endId") && !endId.HasValue))
        {
            return BadRequest(new { message = "Invalid ID range." });
        }

        var result = await _archiveService.VerifyHashChainAsync(startId, endId, cancellationToken);
        return Ok(new { data = result });
    }

    [HttpGet("metadata/{id:int}")]
    public async Task<IActionResult> GetMetadata(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var metadata = await _archiveService.GetMetadataAsync(id, cancellationToken);
        if (metadata == null)
        {
            return NotFound(new { message = "Archive metadata not found." });
        }

        return Ok(new { data = metadata });
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListArchives(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var page = ParseInt(Request.Query["page"], 1);
        var limit = ParseInt(Request.Query["limit"], 50);
        page = Math.Max(1, page);
        limit = Math.Min(200, Math.Max(1, limit));

        var items = await _archiveService.ListAsync(page, limit, cancellationToken);
        var total = await _dbContext.AuditArchiveMetadata.AsNoTracking().CountAsync(cancellationToken);
        var payload = items.Select(item => new
        {
            metadata = item.Metadata,
            log = new
            {
                item.Log.Id,
                item.Log.ActorId,
                item.Log.ActorName,
                item.Log.Action,
                item.Log.EntityType,
                item.Log.EntityId,
                createdAt = item.Log.CreatedAt.ToString("o"),
            }
        }).ToList();

        return Ok(new
        {
            data = payload,
            page,
            pageSize = limit,
            total,
            totalPages = (int)Math.Ceiling(total / (double)limit),
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportLogs(CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminRoleManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var startDate = Request.Query["startDate"].ToString();
        var endDate = Request.Query["endDate"].ToString();
        var isSensitive = Request.Query["isSensitive"].ToString();
        var entityType = Request.Query["entityType"].ToString();
        var entityId = Request.Query["entityId"].ToString();

        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (DateTime.TryParse(startDate, out var start))
        {
            query = query.Where(log => log.CreatedAt >= start);
        }

        if (DateTime.TryParse(endDate, out var end))
        {
            query = query.Where(log => log.CreatedAt <= end);
        }

        if (!string.IsNullOrWhiteSpace(isSensitive))
        {
            var flag = isSensitive == "true" || isSensitive == "1";
            query = query.Where(log => log.IsSensitive == flag);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(log => log.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(log => log.EntityId == entityId);
        }

        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(cancellationToken);

        var parsedLogs = logs.Select(log => new
        {
            log.Id,
            log.ActorId,
            log.ActorName,
            log.EntityType,
            log.EntityId,
            log.Action,
            log.Summary,
            log.IpAddress,
            log.IsSensitive,
            log.Immutable,
            log.HashChainValue,
            createdAt = log.CreatedAt.ToString("o"),
            changes = ParseChanges(log.Changes),
        }).ToList();

        var actor = HttpContext.GetAuthUser();
        if (actor != null)
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "audit_log",
                EntityId = null,
                Action = "export_data",
                Changes = JsonSerializer.Serialize(new
                {
                    count = logs.Count,
                    filters = new { startDate, endDate, isSensitive, entityType, entityId },
                }),
            });
        }

        var exportPayload = new
        {
            exportDate = DateTimeOffset.UtcNow.ToString("o"),
            exportedBy = actor == null ? null : new { id = actor.Id, name = actor.Name },
            filters = new { startDate, endDate, isSensitive, entityType, entityId },
            totalRecords = logs.Count,
            records = parsedLogs,
        };

        var json = JsonSerializer.Serialize(exportPayload, new JsonSerializerOptions { WriteIndented = true });
        var filename = $"audit-logs-export-{DateTimeOffset.UtcNow:yyyy-MM-dd}.json";

        Response.Headers["Content-Type"] = "application/json";
        Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
        return File(Encoding.UTF8.GetBytes(json), "application/json");
    }

    private static object? ParseChanges(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return json;
        }
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int? ParseIntNullable(string? value)
    {
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.All(granted.Contains))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }
}
