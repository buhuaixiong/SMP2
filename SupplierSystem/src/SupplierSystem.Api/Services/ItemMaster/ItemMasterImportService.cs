using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Excel;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.ItemMaster;

public sealed record ItemMasterListQuery(
    string? Fac,
    string? ItemNumber,
    string? Vendor,
    string? SourcingName,
    bool UnassignedOnly,
    int Page,
    int Limit);

public sealed record ItemMasterImportExecutionResult(
    ItemMasterImportBatch Batch,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    bool IsFatal,
    string? FatalMessage);

public sealed class ItemMasterImportService
{
    private static readonly XNamespace SpreadsheetNs =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly IReadOnlyDictionary<string, string[]> HeaderAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["fac"] = ["fac"],
            ["item_number"] = ["itemnumber", "itemno", "itemnum"],
            ["vendor"] = ["vendor"],
            ["sourcing_name"] = ["sourcingname", "sourcename"],
            ["item_description"] = ["itemdescription", "description"],
            ["unit"] = ["unit"],
            ["moq"] = ["moq"],
            ["spq"] = ["spq"],
            ["currency"] = ["currency"],
            ["price_break_1"] = ["pricebreak1", "price1", "pricebreak"],
            ["exchange_rate"] = ["exchangerate"],
            ["vendor_name"] = ["vendorname", "suppliername"],
            ["terms"] = ["terms"],
            ["terms_desc"] = ["termsdesc", "termsdescription"],
            ["company"] = ["company"],
            ["class"] = ["class"],
        };

    private static readonly string[] RequiredColumns = ["fac", "item_number", "vendor", "sourcing_name"];

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ExcelOpenXmlReader _excelReader;
    private readonly ILogger<ItemMasterImportService> _logger;

    public ItemMasterImportService(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        ExcelOpenXmlReader excelReader,
        ILogger<ItemMasterImportService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _excelReader = excelReader;
        _logger = logger;
    }

    public async Task<ItemMasterImportExecutionResult> ImportAsync(
        Stream fileStream,
        string fileName,
        IReadOnlyList<string>? requestedSheets,
        AuthUser actor,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var warnings = new List<string>();
        var errors = new List<string>();
        var batch = new ItemMasterImportBatch
        {
            FileName = string.IsNullOrWhiteSpace(fileName) ? "unknown.xlsx" : fileName.Trim(),
            SheetScope = requestedSheets == null || requestedSheets.Count == 0
                ? "ALL"
                : string.Join(",", requestedSheets.Where(sheet => !string.IsNullOrWhiteSpace(sheet)).Select(sheet => sheet.Trim())),
            Status = "running",
            StartedAt = now,
            ImportedByUserId = actor.Id,
            ImportedByName = actor.Name,
            InsertedCount = 0,
            UpdatedCount = 0,
            WarningCount = 0,
            ErrorCount = 0,
        };

        _dbContext.ItemMasterImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var workbookBuffer = await ReadAllBytesAsync(fileStream, cancellationToken);
            var allSheetNames = ListWorksheetNames(workbookBuffer);
            if (allSheetNames.Count == 0)
            {
                throw new InvalidOperationException("Excel workbook does not contain any worksheets.");
            }

            var targetSheets = ResolveTargetSheets(allSheetNames, requestedSheets);
            batch.SheetScope = string.Join(",", targetSheets);

            var usersByUsername = await _dbContext.Users
                .AsNoTracking()
                .Where(user => !string.IsNullOrWhiteSpace(user.Username))
                .Select(user => new { user.Id, user.Username })
                .ToListAsync(cancellationToken);
            var ownerLookup = usersByUsername
                .Where(user => !string.IsNullOrWhiteSpace(user.Username))
                .ToDictionary(user => user.Username!.Trim(), user => user.Id, StringComparer.OrdinalIgnoreCase);

            var parsedRows = new List<Dictionary<string, string?>>();
            foreach (var sheetName in targetSheets)
            {
                using var sheetStream = new MemoryStream(workbookBuffer, writable: false);
                var sheet = _excelReader.ReadSheet(sheetStream, sheetName);
                if (!sheet.Rows.TryGetValue(6, out var headers))
                {
                    throw new InvalidOperationException($"Worksheet \"{sheetName}\" is missing header row 6.");
                }

                var columnMap = BuildColumnMap(headers);
                ValidateRequiredColumns(columnMap);

                foreach (var row in sheet.Rows.Where(entry => entry.Key > 6).OrderBy(entry => entry.Key))
                {
                    var parsedRow = BuildRowData(row.Value, columnMap);
                    if (IsEmptyRow(parsedRow))
                    {
                        continue;
                    }

                    if (!HasCompositeKey(parsedRow))
                    {
                        errors.Add($"Sheet {sheetName}, row {row.Key}: missing Fac/Item Number/Vendor.");
                        continue;
                    }

                    parsedRow["__sheet"] = sheetName;
                    parsedRow["__row_number"] = row.Key.ToString(CultureInfo.InvariantCulture);
                    parsedRows.Add(parsedRow);
                }
            }

            var deduplicatedRows = CollapseDuplicateRows(parsedRows, warnings);
            var normalizedKeys = deduplicatedRows
                .Select(row => NormalizeCompositeKey(
                    GetRowValue(row, "fac"),
                    GetRowValue(row, "item_number"),
                    GetRowValue(row, "vendor")))
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var facKeys = normalizedKeys
                .Select(key => key.Split('|')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var itemKeys = normalizedKeys
                .Select(key => key.Split('|')[1])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var vendorKeys = normalizedKeys
                .Select(key => key.Split('|')[2])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingRecords = await _dbContext.ItemMasterRecords
                .Where(record =>
                    facKeys.Contains((record.Fac ?? string.Empty).Trim().ToUpper()) &&
                    itemKeys.Contains((record.ItemNumber ?? string.Empty).Trim().ToUpper()) &&
                    vendorKeys.Contains((record.Vendor ?? string.Empty).Trim().ToUpper()))
                .ToListAsync(cancellationToken);

            var existingByKey = existingRecords
                .GroupBy(record => NormalizeCompositeKey(record.Fac, record.ItemNumber, record.Vendor), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

            var insertedCount = 0;
            var updatedCount = 0;
            foreach (var row in deduplicatedRows)
            {
                var fac = GetRowValue(row, "fac");
                var itemNumber = GetRowValue(row, "item_number");
                var vendor = GetRowValue(row, "vendor");
                var key = NormalizeCompositeKey(fac, itemNumber, vendor);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var sourcingName = GetRowValue(row, "sourcing_name");
                var ownerUserId = ResolveOwnerUserId(sourcingName, ownerLookup);
                if (!string.IsNullOrWhiteSpace(sourcingName) && string.IsNullOrWhiteSpace(ownerUserId))
                {
                    warnings.Add(
                        $"Unmatched sourcing name \"{sourcingName}\" at sheet {GetRowValue(row, "__sheet")} row {GetRowValue(row, "__row_number")}.");
                }

                if (existingByKey.TryGetValue(key, out var existing))
                {
                    MapRowToRecord(existing, row, batch.Id, now, ownerUserId, isNewRecord: false);
                    updatedCount += 1;
                }
                else
                {
                    var record = new ItemMasterRecord
                    {
                        Fac = fac ?? string.Empty,
                        ItemNumber = itemNumber ?? string.Empty,
                        Vendor = vendor ?? string.Empty,
                    };

                    MapRowToRecord(record, row, batch.Id, now, ownerUserId, isNewRecord: true);
                    _dbContext.ItemMasterRecords.Add(record);
                    existingByKey[key] = record;
                    insertedCount += 1;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            batch.InsertedCount = insertedCount;
            batch.UpdatedCount = updatedCount;
            batch.WarningCount = warnings.Count;
            batch.ErrorCount = errors.Count;
            batch.Status = errors.Count > 0 ? "partial_success" : "success";
            batch.SummaryJson = JsonSerializer.Serialize(new
            {
                sheets = targetSheets,
                scannedRows = parsedRows.Count,
                deduplicatedRows = deduplicatedRows.Count,
            });
            batch.WarningsJson = warnings.Count > 0 ? JsonSerializer.Serialize(warnings) : null;
            batch.ErrorsJson = errors.Count > 0 ? JsonSerializer.Serialize(errors) : null;
            batch.FinishedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync(actor, batch, warnings, errors);
            return new ItemMasterImportExecutionResult(batch, warnings, errors, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Item master import failed for batch {BatchId}.", batch.Id);
            errors.Add(ex.Message);
            batch.WarningCount = warnings.Count;
            batch.ErrorCount = errors.Count;
            batch.Status = "failed";
            batch.WarningsJson = warnings.Count > 0 ? JsonSerializer.Serialize(warnings) : null;
            batch.ErrorsJson = JsonSerializer.Serialize(errors);
            batch.FinishedAt = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditLogAsync(actor, batch, warnings, errors);
            return new ItemMasterImportExecutionResult(batch, warnings, errors, true, ex.Message);
        }
    }

    public async Task<(IReadOnlyList<ItemMasterRecord> Records, int Total)> GetRecordsAsync(
        ItemMasterListQuery query,
        AuthUser user,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        var scopedQuery = _dbContext.ItemMasterRecords.AsNoTracking().AsQueryable();
        if (!canViewAll)
        {
            scopedQuery = scopedQuery.Where(record => record.OwnerUserId == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(query.Fac))
        {
            var fac = query.Fac.Trim();
            scopedQuery = scopedQuery.Where(record => record.Fac.Contains(fac));
        }

        if (!string.IsNullOrWhiteSpace(query.ItemNumber))
        {
            var itemNumber = query.ItemNumber.Trim();
            scopedQuery = scopedQuery.Where(record => record.ItemNumber.Contains(itemNumber));
        }

        if (!string.IsNullOrWhiteSpace(query.Vendor))
        {
            var vendor = query.Vendor.Trim();
            scopedQuery = scopedQuery.Where(record => record.Vendor.Contains(vendor));
        }

        if (!string.IsNullOrWhiteSpace(query.SourcingName))
        {
            var sourcingName = query.SourcingName.Trim();
            scopedQuery = scopedQuery.Where(record => record.SourcingName != null && record.SourcingName.Contains(sourcingName));
        }

        if (query.UnassignedOnly)
        {
            scopedQuery = scopedQuery.Where(record => string.IsNullOrWhiteSpace(record.OwnerUserId));
        }

        var total = await scopedQuery.CountAsync(cancellationToken);
        var offset = Math.Max(0, (query.Page - 1) * query.Limit);
        var records = await scopedQuery
            .OrderBy(record => record.Fac)
            .ThenBy(record => record.ItemNumber)
            .ThenBy(record => record.Vendor)
            .Skip(offset)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        return (records, total);
    }

    public async Task<(IReadOnlyList<ItemMasterImportBatch> Batches, int Total)> GetBatchesAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedLimit = Math.Min(100, Math.Max(1, limit));

        var query = _dbContext.ItemMasterImportBatches.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var batches = await query
            .OrderByDescending(batch => batch.Id)
            .Skip((normalizedPage - 1) * normalizedLimit)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);
        return (batches, total);
    }

    public Task<ItemMasterImportBatch?> GetBatchByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _dbContext.ItemMasterImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(batch => batch.Id == id, cancellationToken);
    }

    private async Task WriteAuditLogAsync(
        AuthUser actor,
        ItemMasterImportBatch batch,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors)
    {
        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "item_master_import_batch",
                EntityId = batch.Id.ToString(CultureInfo.InvariantCulture),
                Action = "import",
                Summary = $"Item master import {batch.Status}",
                Changes = JsonSerializer.Serialize(new
                {
                    batch.Id,
                    batch.FileName,
                    batch.SheetScope,
                    batch.Status,
                    batch.InsertedCount,
                    batch.UpdatedCount,
                    batch.WarningCount,
                    batch.ErrorCount,
                    warnings,
                    errors,
                }),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write item master audit log for batch {BatchId}.", batch.Id);
        }
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers)
    {
        var aliasIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in HeaderAliases)
        {
            foreach (var name in alias.Value)
            {
                var normalized = NormalizeHeader(name);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    aliasIndex[normalized] = alias.Key;
                }
            }
        }

        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index += 1)
        {
            var normalizedHeader = NormalizeHeader(headers[index]);
            if (string.IsNullOrWhiteSpace(normalizedHeader))
            {
                continue;
            }

            if (!aliasIndex.TryGetValue(normalizedHeader, out var key))
            {
                continue;
            }

            if (!columnMap.ContainsKey(key))
            {
                columnMap[key] = index;
            }
        }

        return columnMap;
    }

    private static void ValidateRequiredColumns(Dictionary<string, int> columnMap)
    {
        var missing = new List<string>();
        foreach (var required in RequiredColumns)
        {
            if (columnMap.ContainsKey(required))
            {
                continue;
            }

            missing.Add(required switch
            {
                "fac" => "Fac",
                "item_number" => "Item Number",
                "vendor" => "Vendor",
                "sourcing_name" => "Sourcing name",
                _ => required,
            });
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Excel is missing required columns: {string.Join(", ", missing)}.");
        }
    }

    private static List<Dictionary<string, string?>> CollapseDuplicateRows(
        IReadOnlyList<Dictionary<string, string?>> rows,
        List<string> warnings)
    {
        var deduplicated = new Dictionary<string, (Dictionary<string, string?> Row, int Index)>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rows.Count; index += 1)
        {
            var row = rows[index];
            var key = NormalizeCompositeKey(
                GetRowValue(row, "fac"),
                GetRowValue(row, "item_number"),
                GetRowValue(row, "vendor"));
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (deduplicated.ContainsKey(key))
            {
                warnings.Add($"Duplicate key \"{key}\" detected; keeping the last row.");
            }

            deduplicated[key] = (row, index);
        }

        return deduplicated.Values
            .OrderBy(entry => entry.Index)
            .Select(entry => entry.Row)
            .ToList();
    }

    private static string? ResolveOwnerUserId(
        string? sourcingName,
        IReadOnlyDictionary<string, string> usersByUsername)
    {
        if (string.IsNullOrWhiteSpace(sourcingName))
        {
            return null;
        }

        var normalized = sourcingName.Trim();
        if (usersByUsername.TryGetValue(normalized, out var userId))
        {
            return userId;
        }

        foreach (var pair in usersByUsername)
        {
            if (string.Equals(pair.Key, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string NormalizeCompositeKey(string? fac, string? itemNumber, string? vendor)
    {
        var left = (fac ?? string.Empty).Trim().ToUpperInvariant();
        var middle = (itemNumber ?? string.Empty).Trim().ToUpperInvariant();
        var right = (vendor ?? string.Empty).Trim().ToUpperInvariant();
        return $"{left}|{middle}|{right}";
    }

    private static Dictionary<string, string?> BuildRowData(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnMap)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in columnMap)
        {
            data[mapping.Key] = GetCellValue(row, mapping.Value);
        }

        return data;
    }

    private static bool IsEmptyRow(IReadOnlyDictionary<string, string?> row)
    {
        return row.Values.All(value => string.IsNullOrWhiteSpace(value));
    }

    private static bool HasCompositeKey(IReadOnlyDictionary<string, string?> row)
    {
        return !string.IsNullOrWhiteSpace(GetRowValue(row, "fac")) &&
               !string.IsNullOrWhiteSpace(GetRowValue(row, "item_number")) &&
               !string.IsNullOrWhiteSpace(GetRowValue(row, "vendor"));
    }

    private static string? GetRowValue(IReadOnlyDictionary<string, string?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? GetCellValue(IReadOnlyList<string> row, int index)
    {
        if (index < 0 || index >= row.Count)
        {
            return null;
        }

        var value = row[index];
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray();
        return new string(chars);
    }

    private static double? ParseNullableDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        return null;
    }

    private static void MapRowToRecord(
        ItemMasterRecord record,
        IReadOnlyDictionary<string, string?> row,
        long batchId,
        string now,
        string? ownerUserId,
        bool isNewRecord)
    {
        record.Fac = GetRowValue(row, "fac") ?? string.Empty;
        record.ItemNumber = GetRowValue(row, "item_number") ?? string.Empty;
        record.Vendor = GetRowValue(row, "vendor") ?? string.Empty;
        record.SourcingName = GetRowValue(row, "sourcing_name");
        record.OwnerUserId = ownerUserId;
        record.OwnerUsernameSnapshot = GetRowValue(row, "sourcing_name");
        record.ItemDescription = GetRowValue(row, "item_description");
        record.Unit = GetRowValue(row, "unit");
        record.Moq = ParseNullableDouble(GetRowValue(row, "moq"));
        record.Spq = ParseNullableDouble(GetRowValue(row, "spq"));
        record.Currency = GetRowValue(row, "currency");
        record.PriceBreak1 = ParseNullableDouble(GetRowValue(row, "price_break_1"));
        record.ExchangeRate = ParseNullableDouble(GetRowValue(row, "exchange_rate"));
        record.VendorName = GetRowValue(row, "vendor_name");
        record.Terms = GetRowValue(row, "terms");
        record.TermsDesc = GetRowValue(row, "terms_desc");
        record.Company = GetRowValue(row, "company");
        record.Class = GetRowValue(row, "class");
        record.RawPayload = JsonSerializer.Serialize(new
        {
            sourceSheet = GetRowValue(row, "__sheet"),
            sourceRow = GetRowValue(row, "__row_number"),
            values = row.Where(entry => !entry.Key.StartsWith("__", StringComparison.Ordinal)).ToDictionary(),
        });
        record.UpdatedAt = now;
        record.LastImportBatchId = batchId;
        if (isNewRecord)
        {
            record.CreatedAt = now;
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var segment))
        {
            return segment.ToArray();
        }

        await using var target = new MemoryStream();
        await stream.CopyToAsync(target, cancellationToken);
        return target.ToArray();
    }

    private static IReadOnlyList<string> ResolveTargetSheets(
        IReadOnlyList<string> allSheets,
        IReadOnlyList<string>? requestedSheets)
    {
        if (requestedSheets == null || requestedSheets.Count == 0)
        {
            return allSheets;
        }

        var requested = requestedSheets
            .Where(sheet => !string.IsNullOrWhiteSpace(sheet))
            .Select(sheet => sheet.Trim())
            .ToList();
        if (requested.Count == 0)
        {
            return allSheets;
        }

        var missing = requested
            .Where(sheet => !allSheets.Contains(sheet, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Worksheet not found: {string.Join(", ", missing)}. Available: {string.Join(", ", allSheets)}.");
        }

        var byName = allSheets.ToDictionary(sheet => sheet, StringComparer.OrdinalIgnoreCase);
        return requested.Select(sheet => byName[sheet]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> ListWorksheetNames(byte[] workbookBuffer)
    {
        using var stream = new MemoryStream(workbookBuffer, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var workbookEntry = archive.GetEntry("xl/workbook.xml");
        if (workbookEntry == null)
        {
            throw new InvalidOperationException("Excel workbook.xml is missing.");
        }

        using var workbookStream = workbookEntry.Open();
        var workbook = XDocument.Load(workbookStream);
        var sheetNames = workbook
            .Root?
            .Element(SpreadsheetNs + "sheets")?
            .Elements(SpreadsheetNs + "sheet")
            .Select(sheet => sheet.Attribute("name")?.Value?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList() ?? new List<string>();

        return sheetNames;
    }
}
