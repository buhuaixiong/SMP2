using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class BackupManagementController : NodeControllerBase
{
    private readonly BackupService _backupService;
    private readonly ArchiveService _archiveService;
    private readonly BackupAlertService _alertService;
    private readonly BackupScheduler _backupScheduler;
    private readonly ILogger<BackupManagementController> _logger;

    public BackupManagementController(
        BackupService backupService,
        ArchiveService archiveService,
        BackupAlertService alertService,
        BackupScheduler backupScheduler,
        IWebHostEnvironment environment,
        ILogger<BackupManagementController> logger) : base(environment)
    {
        _backupService = backupService;
        _archiveService = archiveService;
        _alertService = alertService;
        _backupScheduler = backupScheduler;
        _logger = logger;
    }

    [HttpGet("backups")]
    public async Task<IActionResult> GetBackups(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var stats = _backupService.GetBackupStats();
            var history = await _backupService.GetBackupHistoryAsync(20, cancellationToken);
            var schedulerStatus = _backupScheduler.GetStatus();

            return Ok(new
            {
                success = true,
                data = new
                {
                    stats,
                    history,
                    scheduler = schedulerStatus,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error getting backups.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("backups/stats")]
    public IActionResult GetBackupStats(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var stats = _backupService.GetBackupStats();
            return Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error getting backup stats.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("backups/history")]
    public async Task<IActionResult> GetBackupHistory(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var limit = ParseInt(Request.Query["limit"], 50);
            limit = Math.Min(200, Math.Max(1, limit));
            var history = await _backupService.GetBackupHistoryAsync(limit, cancellationToken);
            return Ok(new { success = true, data = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error getting backup history.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("backups/manual")]
    public IActionResult TriggerManualBackup([FromQuery] bool full = false)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var status = _backupScheduler.GetStatus();
        if (status.IsRunning.TryGetValue("dailyBackup", out var isRunning) && isRunning)
        {
            return StatusCode(409, new { success = false, message = "Backup is already running. Please try again later." });
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _backupService.TriggerManualBackupAsync(full, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BackupManagement] Manual {Type} backup failed.", full ? "full" : "differential");
            }
        });

        return Ok(new { success = true, message = $"{(full ? "Full" : "Differential")} backup task triggered. Check results later." });
    }

    [HttpPost("backups/verify/{filename}")]
    public async Task<IActionResult> VerifyBackup(string filename, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var type = Request.Query["type"].ToString();
            var config = BackupConfig.Load();
            var root = type switch
            {
                "weekly" => config.Paths.Weekly,
                "monthly" => config.Paths.Monthly,
                "yearly" => config.Paths.Yearly,
                "differential" => config.Paths.Differential,
                _ => config.Paths.Daily,
            };

            var backupPath = Path.Combine(root, filename);
            var result = await _backupService.VerifyBackupAsync(backupPath, config.Verification.MinSizeBytes, cancellationToken);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error verifying backup.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("backups/scheduler")]
    public IActionResult GetSchedulerStatus()
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var status = _backupScheduler.GetStatus();
            return Ok(new { success = true, data = status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error getting scheduler status.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("archives")]
    public IActionResult GetArchiveStats()
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var stats = _archiveService.GetArchiveStats();
            return Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error getting archive stats.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("archives/rfq")]
    public IActionResult GetRfqArchives([FromQuery] int? year, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!year.HasValue)
        {
            return BadRequest(new { success = false, message = "Year is required." });
        }

        try
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Min(200, Math.Max(1, pageSize));
            var offset = (normalizedPage - 1) * normalizedPageSize;

            var parameters = new List<object?>();
            var sql = new StringBuilder("SELECT * FROM rfqs WHERE 1=1");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var like = $"%{keyword.Trim()}%";
                var titleParam = AddParam(parameters, like);
                var descriptionParam = AddParam(parameters, like);
                sql.Append($" AND (title LIKE {titleParam} OR description LIKE {descriptionParam})");
            }

            var limitParam = AddParam(parameters, normalizedPageSize);
            var offsetParam = AddParam(parameters, offset);
            sql.Append($" ORDER BY created_at DESC LIMIT {limitParam} OFFSET {offsetParam}");

            var items = _archiveService.QueryArchive("rfq", year.Value, sql.ToString(), parameters);

            var countParameters = new List<object?>();
            var countSql = new StringBuilder("SELECT COUNT(*) as total FROM rfqs WHERE 1=1");
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var like = $"%{keyword.Trim()}%";
                var titleParam = AddParam(countParameters, like);
                var descriptionParam = AddParam(countParameters, like);
                countSql.Append($" AND (title LIKE {titleParam} OR description LIKE {descriptionParam})");
            }

            var total = _archiveService.QueryArchiveScalar("rfq", year.Value, countSql.ToString(), countParameters);

            return Ok(new
            {
                success = true,
                data = new
                {
                    items,
                    total,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    year,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error querying RFQ archives.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("archives/suppliers")]
    public IActionResult GetSupplierArchives([FromQuery] int? year, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!year.HasValue)
        {
            return BadRequest(new { success = false, message = "Year is required." });
        }

        try
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Min(200, Math.Max(1, pageSize));
            var offset = (normalizedPage - 1) * normalizedPageSize;

            var parameters = new List<object?>();
            var sql = new StringBuilder("SELECT * FROM suppliers WHERE 1=1");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var like = $"%{keyword.Trim()}%";
                var nameParam = AddParam(parameters, like);
                var idParam = AddParam(parameters, like);
                sql.Append($" AND (companyName LIKE {nameParam} OR companyId LIKE {idParam})");
            }

            var limitParam = AddParam(parameters, normalizedPageSize);
            var offsetParam = AddParam(parameters, offset);
            sql.Append($" ORDER BY createdAt DESC LIMIT {limitParam} OFFSET {offsetParam}");

            var items = _archiveService.QueryArchive("supplier", year.Value, sql.ToString(), parameters);

            return Ok(new
            {
                success = true,
                data = new
                {
                    items,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    year,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error querying supplier archives.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("archives/audit")]
    public IActionResult GetAuditArchives([FromQuery] int? year, [FromQuery] string? keyword, [FromQuery] string? actorId, [FromQuery] string? entityType, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!year.HasValue)
        {
            return BadRequest(new { success = false, message = "Year is required." });
        }

        try
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Min(200, Math.Max(1, pageSize));
            var offset = (normalizedPage - 1) * normalizedPageSize;

            var parameters = new List<object?>();
            var sql = new StringBuilder("SELECT * FROM audit_log WHERE 1=1");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var like = $"%{keyword.Trim()}%";
                var summaryParam = AddParam(parameters, like);
                var changesParam = AddParam(parameters, like);
                sql.Append($" AND (summary LIKE {summaryParam} OR changes LIKE {changesParam})");
            }

            if (!string.IsNullOrWhiteSpace(actorId))
            {
                var actorParam = AddParam(parameters, actorId.Trim());
                sql.Append($" AND actorId = {actorParam}");
            }

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                var typeParam = AddParam(parameters, entityType.Trim());
                sql.Append($" AND entityType = {typeParam}");
            }

            var limitParam = AddParam(parameters, normalizedPageSize);
            var offsetParam = AddParam(parameters, offset);
            sql.Append($" ORDER BY createdAt DESC LIMIT {limitParam} OFFSET {offsetParam}");

            var items = _archiveService.QueryArchive("audit", year.Value, sql.ToString(), parameters);

            return Ok(new
            {
                success = true,
                data = new
                {
                    items,
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    year,
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error querying audit archives.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("archives/manual")]
    public IActionResult TriggerManualArchive()
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var status = _backupScheduler.GetStatus();
        if (status.IsRunning.TryGetValue("archiveSeparation", out var running) && running)
        {
            return StatusCode(409, new { success = false, message = "Archive task is already running. Please try again later." });
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _backupScheduler.TriggerManualArchiveAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BackupManagement] Manual archive failed.");
            }
        });

        return Ok(new { success = true, message = "Archive task triggered. Check results later." });
    }

    [HttpGet("backups/alerts")]
    public async Task<IActionResult> GetBackupAlerts(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        try
        {
            var unresolved = string.Equals(Request.Query["unresolved"], "true", StringComparison.OrdinalIgnoreCase);
            var limit = unresolved ? 50 : 100;
            var alerts = await _alertService.GetUnresolvedAsync(limit, cancellationToken);
            return Ok(new { success = true, data = alerts });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupManagement] Error getting alerts.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    [HttpPost("backups/alerts/{id:int}/resolve")]
    public async Task<IActionResult> ResolveAlert(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAdmin(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        var resolvedBy = user?.Id ?? "admin";
        var success = await _alertService.ResolveAsync(id, resolvedBy, cancellationToken);

        if (!success)
        {
            return BadRequest(new { success = false, message = "Failed to resolve alert." });
        }

        return Ok(new { success = true, message = "Alert resolved." });
    }

    private static IActionResult? RequireAdmin(AuthUser? user)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        if (!string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string AddParam(List<object?> parameters, object? value)
    {
        var name = $"@p{parameters.Count}";
        parameters.Add(value);
        return name;
    }
}
