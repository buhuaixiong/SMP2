using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services.ItemMaster;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/item-master")]
public sealed class ItemMasterController : NodeControllerBase
{
    private static readonly string[] SupportedExtensions = [".xlsx", ".xlsm"];

    private readonly ItemMasterImportService _itemMasterImportService;

    public ItemMasterController(
        ItemMasterImportService itemMasterImportService,
        IWebHostEnvironment environment) : base(environment)
    {
        _itemMasterImportService = itemMasterImportService;
    }

    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile? file,
        [FromForm] List<string>? sheets,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(user, Permissions.ItemMasterImportManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Excel file is required." });
        }

        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only .xlsx and .xlsm files are supported." });
        }

        await using var stream = file.OpenReadStream();
        IReadOnlyList<string> sheetSelection =
            sheets ?? (IReadOnlyList<string>)Array.Empty<string>();
        var importResult = await _itemMasterImportService.ImportAsync(
            stream,
            file.FileName,
            sheetSelection,
            user!,
            cancellationToken);

        var payload = new
        {
            batchId = importResult.Batch.Id,
            status = importResult.Batch.Status,
            insertedCount = importResult.Batch.InsertedCount,
            updatedCount = importResult.Batch.UpdatedCount,
            warningCount = importResult.Batch.WarningCount,
            errorCount = importResult.Batch.ErrorCount,
            warnings = importResult.Warnings,
            errors = importResult.Errors,
            fatalMessage = importResult.FatalMessage,
        };

        if (importResult.IsFatal)
        {
            return BadRequest(payload);
        }

        return Ok(payload);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? fac,
        [FromQuery] string? itemNumber,
        [FromQuery] string? vendor,
        [FromQuery] string? sourcingName,
        [FromQuery] bool unassignedOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var canViewAll = HasPermission(user, Permissions.ItemMasterViewAll);
        var canViewOwn = HasPermission(user, Permissions.ItemMasterViewOwn);
        if (!canViewAll && !canViewOwn)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied." });
        }

        var normalizedPage = Math.Max(1, page);
        var normalizedLimit = Math.Min(100, Math.Max(1, limit));
        var query = new ItemMasterListQuery(
            fac,
            itemNumber,
            vendor,
            sourcingName,
            unassignedOnly,
            normalizedPage,
            normalizedLimit);

        var (records, total) = await _itemMasterImportService.GetRecordsAsync(
            query,
            user,
            canViewAll,
            cancellationToken);

        return Ok(new
        {
            data = records.Select(record => new
            {
                id = record.Id,
                fac = record.Fac,
                itemNumber = record.ItemNumber,
                vendor = record.Vendor,
                sourcingName = record.SourcingName,
                ownerUserId = record.OwnerUserId,
                ownerUsernameSnapshot = record.OwnerUsernameSnapshot,
                itemDescription = record.ItemDescription,
                unit = record.Unit,
                moq = record.Moq,
                spq = record.Spq,
                currency = record.Currency,
                priceBreak1 = record.PriceBreak1,
                exchangeRate = record.ExchangeRate,
                vendorName = record.VendorName,
                terms = record.Terms,
                termsDesc = record.TermsDesc,
                company = record.Company,
                @class = record.Class,
                updatedAt = record.UpdatedAt,
                lastImportBatchId = record.LastImportBatchId,
            }),
            pagination = new
            {
                page = normalizedPage,
                limit = normalizedLimit,
                total,
                totalPages = (int)Math.Ceiling(total / (double)normalizedLimit),
            },
        });
    }

    [HttpGet("import-batches")]
    public async Task<IActionResult> ListImportBatches(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(user, Permissions.ItemMasterViewAll);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var normalizedPage = Math.Max(1, page);
        var normalizedLimit = Math.Min(100, Math.Max(1, limit));
        var (batches, total) = await _itemMasterImportService.GetBatchesAsync(
            normalizedPage,
            normalizedLimit,
            cancellationToken);

        return Ok(new
        {
            data = batches.Select(batch => new
            {
                id = batch.Id,
                fileName = batch.FileName,
                sheetScope = batch.SheetScope,
                status = batch.Status,
                startedAt = batch.StartedAt,
                finishedAt = batch.FinishedAt,
                importedByUserId = batch.ImportedByUserId,
                importedByName = batch.ImportedByName,
                insertedCount = batch.InsertedCount,
                updatedCount = batch.UpdatedCount,
                warningCount = batch.WarningCount,
                errorCount = batch.ErrorCount,
            }),
            pagination = new
            {
                page = normalizedPage,
                limit = normalizedLimit,
                total,
                totalPages = (int)Math.Ceiling(total / (double)normalizedLimit),
            },
        });
    }

    [HttpGet("import-batches/{id:long}")]
    public async Task<IActionResult> GetImportBatch(long id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(user, Permissions.ItemMasterViewAll);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var batch = await _itemMasterImportService.GetBatchByIdAsync(id, cancellationToken);
        if (batch == null)
        {
            return NotFound(new { message = "Import batch not found." });
        }

        return Ok(new
        {
            data = new
            {
                id = batch.Id,
                fileName = batch.FileName,
                sheetScope = batch.SheetScope,
                status = batch.Status,
                startedAt = batch.StartedAt,
                finishedAt = batch.FinishedAt,
                importedByUserId = batch.ImportedByUserId,
                importedByName = batch.ImportedByName,
                insertedCount = batch.InsertedCount,
                updatedCount = batch.UpdatedCount,
                warningCount = batch.WarningCount,
                errorCount = batch.ErrorCount,
                summary = ParseJsonObject(batch.SummaryJson),
                warnings = ParseJsonArray(batch.WarningsJson),
                errors = ParseJsonArray(batch.ErrorsJson),
            },
        });
    }

    private static IActionResult? RequireAnyPermission(AuthUser? user, params string[] requiredPermissions)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        if (requiredPermissions.Length == 0)
        {
            return null;
        }

        if (requiredPermissions.Any(permission => HasPermission(user, permission)))
        {
            return null;
        }

        return new ObjectResult(new { message = "Access denied." })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }

    private static bool HasPermission(AuthUser user, string permission)
    {
        return user.Permissions?.Contains(permission, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static object? ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return json;
        }
    }

    private static IReadOnlyList<string> ParseJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json);
            return parsed ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch
        {
            return [json];
        }
    }
}
