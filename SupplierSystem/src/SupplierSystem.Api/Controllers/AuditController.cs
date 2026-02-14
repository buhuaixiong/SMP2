using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/audit")]
public sealed class AuditController : NodeControllerBase
{
    private readonly SupplierSystemDbContext _dbContext;

    public AuditController(SupplierSystemDbContext dbContext, IWebHostEnvironment environment) : base(environment)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Query audit logs with filtering and pagination
    /// </summary>
    /// <param name="entityType">Filter by entity type</param>
    /// <param name="entityId">Filter by entity ID</param>
    /// <param name="actorId">Filter by actor ID</param>
    /// <param name="ipAddress">Filter by IP address</param>
    /// <param name="keyword">Search keyword</param>
    /// <param name="startDate">Start date filter</param>
    /// <param name="endDate">End date filter</param>
    /// <param name="isSensitive">Filter by sensitivity</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="limit">Page size (max: 200, default: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated audit logs</returns>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? actorId,
        [FromQuery] string? ipAddress,
        [FromQuery] string? keyword,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] string? isSensitive,
        [FromQuery] int? page,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var permissionResult = RequireAuditAccess(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var pageValue = Math.Max(1, page ?? 1);
        var limitValue = Math.Min(200, Math.Max(1, limit ?? 50));
        var offset = (pageValue - 1) * limitValue;

        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(log => log.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(log => log.EntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(actorId))
        {
            query = query.Where(log => log.ActorId == actorId);
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            query = query.Where(log => log.IpAddress == ipAddress);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(log =>
                (log.Action != null && log.Action.Contains(keyword)) ||
                (log.ActorName != null && log.ActorName.Contains(keyword)) ||
                (log.Changes != null && log.Changes.Contains(keyword)) ||
                (log.Summary != null && log.Summary.Contains(keyword)));
        }

        if (TryParseDate(startDate, out var start))
        {
            query = query.Where(log => log.CreatedAt >= start);
        }

        if (TryParseDate(endDate, out var end))
        {
            query = query.Where(log => log.CreatedAt <= end);
        }

        if (TryParseBool(isSensitive, out var sensitive))
        {
            query = query.Where(log => log.IsSensitive == sensitive);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip(offset)
            .Take(limitValue)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = items,
            page = pageValue,
            pageSize = limitValue,
            total,
            totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)limitValue)
        });
    }

    [HttpGet("diagnostics/aggregator")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public IActionResult GetAggregatorDiagnostics()
    {
        var permissionResult = RequireAuditAccess(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        return Ok(new { data = BuildAggregatorDiagnostics() });
    }

    /// <summary>
    /// Get a specific audit log by ID
    /// </summary>
    /// <param name="id">Audit log ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audit log details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditLog(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAuditAccess(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var log = await _dbContext.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (log == null)
        {
            return NotFound(new { message = "Audit record not found." });
        }

        return Ok(new { data = log });
    }

    private static IActionResult? RequireAuditAccess(AuthUser? user)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var allowed = granted.Contains(Permissions.AdminRoleManage)
                      || granted.Contains(Permissions.ProcurementDirectorReportsView)
                      || granted.Contains(Permissions.FinanceDirectorRiskMonitor);

        if (!allowed && string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            allowed = true;
        }

        return allowed ? null : new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
    }

    private static object BuildAggregatorDiagnostics()
    {
        return new
        {
            enabled = false,
            backends = new { elastic = false, loki = false },
            pushes = new
            {
                total = 0,
                success = 0,
                failure = 0,
                lastSuccessAt = (string?)null,
                lastFailure = (object?)null,
                backends = new Dictionary<string, object?>()
            },
            queries = new
            {
                total = 0,
                success = 0,
                failure = 0,
                skipped = 0,
                last = (object?)null,
                backends = new Dictionary<string, object?>()
            }
        };
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

    private static bool TryParseDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParse(value, out result);
    }
}
