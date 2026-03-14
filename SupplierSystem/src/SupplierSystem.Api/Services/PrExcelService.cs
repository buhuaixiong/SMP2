using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Api.Controllers;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using DomainRfq = SupplierSystem.Domain.Entities.Rfq;

namespace SupplierSystem.Api.Services;

public sealed record PrExcelOptions
{
    public string? PrNumber { get; init; }
    public string? Department { get; init; }
    public string? AccountNo { get; init; }
}

public sealed record PrExcelUserContext
{
    public string Id { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string? Username { get; init; }
}

public sealed record PrExcelGenerationResult(byte[] Buffer, string PrNumber, int LineItemCount);

public sealed class PrExcelService(
    SupplierSystemDbContext dbContext,
    IWebHostEnvironment environment,
    ILogger<PrExcelService> logger)
{
    private const string TemplateFileName = "InquiryIndirectmaterialPR.xlsm";
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "pending_po", "completed" };

    private static readonly Dictionary<string, double> CurrencyRates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CNY"] = 7.2,
            ["RMB"] = 7.2,
            ["USD"] = 1,
            ["EUR"] = 0.91,
            ["GBP"] = 0.79,
            ["JPY"] = 149.5,
            ["HKD"] = 7.8,
            ["THB"] = 35.5
        };

    private readonly SupplierSystemDbContext _dbContext = dbContext;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<PrExcelService> _logger = logger;

    public async Task<PrExcelGenerationResult> GenerateAsync(
        long rfqId,
        IReadOnlyList<long> lineItemIds,
        PrExcelOptions options,
        PrExcelUserContext user,
        CancellationToken cancellationToken)
    {
        var rfq = await GetRfqAsync(rfqId, cancellationToken);
        if (rfq == null)
        {
            throw new InvalidOperationException($"RFQ {rfqId} not found");
        }

        var lineItems = await GetLineItemsAsync(rfqId, lineItemIds, cancellationToken);
        if (lineItems.Count == 0)
        {
            throw new InvalidOperationException("No line items found for generation");
        }

        var prNumber = await ResolvePrNumberAsync(rfq, options, cancellationToken);
        var templatePath = ResolveTemplatePath();
        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException("PR Excel template is missing");
        }

        var templateBuffer = await File.ReadAllBytesAsync(templatePath, cancellationToken);
        var outputBuffer = await ApplyTemplateAsync(templateBuffer, rfq, lineItems, options, user, prNumber, cancellationToken);

        await LogAuditAsync(rfqId, lineItems.Count, prNumber, user, outputBuffer.Length, cancellationToken);
        return new PrExcelGenerationResult(outputBuffer, prNumber, lineItems.Count);
    }

    private async Task<DomainRfq?> GetRfqAsync(long rfqId, CancellationToken cancellationToken)
    {
        return await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
    }

    private async Task<List<PrLineItem>> GetLineItemsAsync(
        long rfqId,
        IReadOnlyList<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        if (lineItemIds.Count == 0)
        {
            return new List<PrLineItem>();
        }

        var uniqueIds = lineItemIds.Distinct().ToList();
        var approvalLookup = await _dbContext.RfqLineItemApprovalHistories.AsNoTracking()
            .Where(h => uniqueIds.Contains(h.RfqLineItemId) &&
                        (h.Decision == "approved" || h.Decision == "confirmed"))
            .GroupBy(h => h.RfqLineItemId)
            .Select(g => new { LineItemId = g.Key, ApprovedAt = g.Max(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.LineItemId, x => x.ApprovedAt, cancellationToken);

        var lineItems = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                               join q in _dbContext.Quotes.AsNoTracking()
                                   on li.SelectedQuoteId equals (long?)q.Id into quotesWithSelected
                               from q in quotesWithSelected.DefaultIfEmpty()
                               join qli in _dbContext.QuoteLineItems.AsNoTracking()
                                   on new { QuoteId = li.SelectedQuoteId, LineItemId = li.Id }
                                   equals new { QuoteId = (long?)qli.QuoteId, LineItemId = qli.RfqLineItemId }
                               where li.RfqId == rfqId
                                     && uniqueIds.Contains(li.Id)
                                     && li.Status != null
                                     && AllowedStatuses.Contains(li.Status)
                                     && li.SelectedQuoteId != null
                                     && q != null
                                     && q.Status == "selected"
                               orderby li.LineNumber
                               select new PrLineItem
                               {
                                   LineItemId = li.Id,
                                   LineNumber = li.LineNumber,
                                   ItemName = li.ItemName ?? string.Empty,
                                   Specifications = li.Specifications,
                                   Brand = li.Brand,
                                   QuoteBrand = qli.Brand,
                                   Quantity = Convert.ToDouble(li.Quantity),
                                   Unit = li.Unit,
                                   LineCurrency = li.Currency,
                                   SelectedQuoteId = li.SelectedQuoteId,
                                   UnitPrice = qli.UnitPrice.HasValue ? Convert.ToDouble(qli.UnitPrice.Value) : null,
                                   TotalPrice = qli.TotalPrice.HasValue ? Convert.ToDouble(qli.TotalPrice.Value) : null,
                                   QuoteCurrency = q.Currency,
                                   SupplierId = q.SupplierId,
                                   ApprovalCompletedAt = null
                               })
            .ToListAsync(cancellationToken);

        if (lineItems.Count == 0)
        {
            return lineItems;
        }

        foreach (var item in lineItems)
        {
            if (approvalLookup.TryGetValue(item.LineItemId, out var approvedAt))
            {
                item.ApprovalCompletedAt = approvedAt;
            }
        }

        var supplierIds = lineItems.Select(li => li.SupplierId)
            .Where(id => id.HasValue && id.Value > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (supplierIds.Count > 0)
        {
            var suppliers = await _dbContext.Suppliers.AsNoTracking()
                .Where(s => supplierIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken);

            foreach (var item in lineItems)
            {
                if (!item.SupplierId.HasValue || !suppliers.TryGetValue(item.SupplierId.Value, out var supplier))
                {
                    continue;
                }

                item.SupplierName = supplier.CompanyName;
                item.SupplierCode = supplier.CompanyId;
                item.VendorCode = supplier.SupplierCode ?? supplier.CompanyId;
            }
        }

        return lineItems;
    }

    private async Task<byte[]> ApplyTemplateAsync(
        byte[] templateBuffer,
        DomainRfq rfq,
        List<PrLineItem> lineItems,
        PrExcelOptions options,
        PrExcelUserContext user,
        string prNumber,
        CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        await memory.WriteAsync(templateBuffer, cancellationToken);
        memory.Position = 0;

        using (var archive = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("xl/worksheets/sheet2.xml");
            if (entry == null)
            {
                throw new InvalidOperationException("PR Excel template worksheet not found");
            }

            string sheetXml;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                sheetXml = await reader.ReadToEndAsync();
            }

            var modified = ModifySheetXml(sheetXml, rfq, lineItems, options, user, prNumber);
            entry.Delete();

            var updatedEntry = archive.CreateEntry("xl/worksheets/sheet2.xml");
            await using var writer = new StreamWriter(updatedEntry.Open(), new UTF8Encoding(false));
            await writer.WriteAsync(modified);
        }

        return memory.ToArray();
    }

    private string ModifySheetXml(
        string sheetXml,
        DomainRfq rfq,
        List<PrLineItem> lineItems,
        PrExcelOptions options,
        PrExcelUserContext user,
        string prNumber)
    {
        var department = string.IsNullOrWhiteSpace(options.Department)
            ? rfq.RequestingDepartment ?? string.Empty
            : options.Department.Trim();
        var userName = ResolveUserName(user);

        sheetXml = SetCellValue(sheetXml, "B2", prNumber);
        sheetXml = SetCellValue(sheetXml, "B5", department);

        var today = ExcelDateSerial(DateTime.Now);
        sheetXml = SetCellValue(sheetXml, "B6", today, "n");
        sheetXml = SetCellValue(sheetXml, "D6", today, "n");

        sheetXml = SetCellValue(sheetXml, "B8", userName);

        var currentRow = 16;
        foreach (var item in lineItems.OrderBy(li => li.LineNumber))
        {
            sheetXml = FillLineItemXml(sheetXml, currentRow, item, rfq, options, user, prNumber);
            currentRow++;
        }

        return sheetXml;
    }

    private string FillLineItemXml(
        string xml,
        int rowIndex,
        PrLineItem item,
        DomainRfq rfq,
        PrExcelOptions options,
        PrExcelUserContext user,
        string prNumber)
    {
        var currency = item.QuoteCurrency ?? item.LineCurrency ?? "CNY";
        var vendorCode = item.VendorCode ?? item.SupplierCode ?? string.Empty;
        var unitPrice = item.UnitPrice ?? 0;
        var totalPrice = item.TotalPrice ?? 0;
        var department = string.IsNullOrWhiteSpace(options.Department)
            ? rfq.RequestingDepartment ?? string.Empty
            : options.Department!.Trim();
        var userName = ResolveUserName(user);
        var approvalDate = ExcelDateSerial(ResolveApprovalDate(item));

        var cells = new Dictionary<string, object?>
        {
            ["A"] = prNumber,
            ["B"] = item.LineNumber,
            ["C"] = department,
            ["D"] = string.Empty,
            ["E"] = FormatItemDescription(item),
            ["F"] = approvalDate,
            ["G"] = userName,
            ["H"] = item.Quantity,
            ["I"] = string.IsNullOrWhiteSpace(item.Unit) ? "PCS" : item.Unit,
            ["J"] = options.AccountNo ?? string.Empty,
            ["K"] = string.Empty,
            ["L"] = item.Quantity,
            ["M"] = 0,
            ["N"] = userName,
            ["O"] = currency,
            ["P"] = unitPrice,
            ["Q"] = ConvertToUsd(unitPrice, currency),
            ["R"] = totalPrice,
            ["S"] = ConvertToUsd(totalPrice, currency),
            ["T"] = approvalDate,
            ["U"] = approvalDate,
            ["V"] = vendorCode,
            ["W"] = item.SupplierName ?? string.Empty,
            ["X"] = string.Empty,
            ["Y"] = string.Empty
        };

        var numericColumns = new HashSet<string> { "B", "F", "H", "L", "M", "P", "Q", "R", "S", "T", "U" };

        foreach (var (column, value) in cells)
        {
            var cellRef = $"{column}{rowIndex}";
            var type = numericColumns.Contains(column) ? "n" : "s";
            xml = SetCellValue(xml, cellRef, value, type);
        }

        return xml;
    }

    private static DateTime ResolveApprovalDate(PrLineItem item)
    {
        var parsed = ControllerHelpers.ParseDateTime(item.ApprovalCompletedAt);
        return parsed ?? DateTime.Now;
    }

    private static string ResolveUserName(PrExcelUserContext user)
    {
        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            return user.Name!;
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return user.Username!;
        }

        return user.Id;
    }

    private async Task<string> ResolvePrNumberAsync(DomainRfq rfq, PrExcelOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.PrNumber))
        {
            return options.PrNumber.Trim();
        }

        return await GeneratePrNumberAsync(rfq, options, cancellationToken);
    }

    private async Task<string> GeneratePrNumberAsync(DomainRfq rfq, PrExcelOptions options, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var dateStr = today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var department = string.IsNullOrWhiteSpace(options.Department)
            ? rfq.RequestingDepartment ?? "IT"
            : options.Department!.Trim();

        var dept = department.ToUpperInvariant();
        var start = today.Date;
        var end = start.AddDays(1);

        var existingCount = await _dbContext.AuditLogs.AsNoTracking()
            .CountAsync(log => log.Action == "generate_pr_excel" &&
                               log.CreatedAt >= start &&
                               log.CreatedAt < end,
                cancellationToken);

        var seq = (existingCount + 1).ToString("D3", CultureInfo.InvariantCulture);
        return $"HZ_{dept}_{dateStr}{seq}";
    }

    private static string FormatItemDescription(PrLineItem item)
    {
        var desc = item.ItemName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(item.Specifications))
        {
            desc = $"{desc} {item.Specifications}";
        }

        var brand = item.QuoteBrand ?? item.Brand;
        if (!string.IsNullOrWhiteSpace(brand))
        {
            desc = $"{desc} [Brand: {brand}]";
        }

        return desc.Trim();
    }

    private static double ConvertToUsd(double amount, string currency)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount == 0)
        {
            return 0;
        }

        var normalized = currency?.Trim().ToUpperInvariant() ?? "CNY";
        if (!CurrencyRates.TryGetValue(normalized, out var rate))
        {
            rate = 1;
        }

        if (rate <= 0)
        {
            return 0;
        }

        var usdAmount = amount / rate;
        return Math.Round(usdAmount, 2, MidpointRounding.AwayFromZero);
    }

    private static int ExcelDateSerial(DateTime date)
    {
        var localDate = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
        var epoch = new DateTime(1899, 12, 30);
        return (int)Math.Floor((localDate - epoch).TotalDays);
    }

    private static string SetCellValue(string xml, string cellRef, object? value, string type = "s")
    {
        if (string.IsNullOrWhiteSpace(cellRef))
        {
            return xml;
        }

        var cellPattern = new Regex($"<c r=\"{cellRef}\"[^>]*>.*?</c>", RegexOptions.Singleline);
        var cellXml = BuildCellXml(cellRef, value, type);

        if (cellPattern.IsMatch(xml))
        {
            return cellPattern.Replace(xml, cellXml, 1, 0);
        }

        var rowMatch = Regex.Match(cellRef, "\\d+");
        if (!rowMatch.Success)
        {
            return xml;
        }

        var rowNumber = rowMatch.Value;
        var updated = InsertCellIntoRow(xml, rowNumber, cellXml);
        if (updated != null)
        {
            return updated;
        }

        return InsertRowWithCell(xml, rowNumber, cellXml);
    }

    private static string BuildCellXml(string cellRef, object? value, string type)
    {
        if (string.Equals(type, "n", StringComparison.OrdinalIgnoreCase))
        {
            var numericValue = 0.0;
            if (value != null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                numericValue = parsed;
            }

            return $"<c r=\"{cellRef}\" t=\"n\"><v>{numericValue.ToString(CultureInfo.InvariantCulture)}</v></c>";
        }

        var escapedValue = EscapeXmlValue(value);
        return $"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{escapedValue}</t></is></c>";
    }

    private static string EscapeXmlValue(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return value.ToString()!
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string? InsertCellIntoRow(string xml, string rowNumber, string cellXml)
    {
        var rowPattern = new Regex($"(<row[^>]*r=\"{rowNumber}\"[^>]*>)([\\s\\S]*?)(</row>)", RegexOptions.Singleline);
        if (!rowPattern.IsMatch(xml))
        {
            return null;
        }

        return rowPattern.Replace(xml,
            match => $"{match.Groups[1].Value}{match.Groups[2].Value}{cellXml}{match.Groups[3].Value}",
            1,
            0);
    }

    private static string InsertRowWithCell(string xml, string rowNumber, string cellXml)
    {
        if (!int.TryParse(rowNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetRow))
        {
            return xml;
        }

        var newRow = $"<row r=\"{rowNumber}\" spans=\"1:25\">{cellXml}</row>";
        var rowPattern = new Regex("<row r=\"(\\d+)\"[^>]*>[\\s\\S]*?</row>");
        var matches = rowPattern.Matches(xml);
        var insertIndex = -1;
        var lastRowEnd = -1;

        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var existingRow))
            {
                continue;
            }

            var rowStart = match.Index;
            var rowEnd = match.Index + match.Length;
            lastRowEnd = rowEnd;

            if (existingRow > targetRow)
            {
                insertIndex = rowStart;
                break;
            }
        }

        if (insertIndex >= 0)
        {
            return $"{xml[..insertIndex]}{newRow}{xml[insertIndex..]}";
        }

        if (lastRowEnd >= 0)
        {
            return $"{xml[..lastRowEnd]}{newRow}{xml[lastRowEnd..]}";
        }

        return xml.Replace("</sheetData>", $"{newRow}</sheetData>", StringComparison.Ordinal);
    }

    private string ResolveTemplatePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("PR_EXCEL_TEMPLATE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath.Trim());
        }

        var repoRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", ".."));
        var appTemplatePath = Path.Combine(repoRoot, "app", "template", TemplateFileName);
        if (File.Exists(appTemplatePath))
        {
            return appTemplatePath;
        }

        return Path.Combine(_environment.ContentRootPath, "templates", TemplateFileName);
    }

    private async Task LogAuditAsync(
        long rfqId,
        int lineItemCount,
        string prNumber,
        PrExcelUserContext user,
        int fileSize,
        CancellationToken cancellationToken)
    {
        try
        {
            var changes = new
            {
                lineItemCount,
                prNumber,
                fileSizeKB = Math.Round(fileSize / 1024.0, 2, MidpointRounding.AwayFromZero),
                method = "direct_xml_modification"
            };

            var entry = new AuditLog
            {
                EntityType = "rfq",
                EntityId = rfqId.ToString(CultureInfo.InvariantCulture),
                Action = "generate_pr_excel",
                ActorId = user.Id,
                ActorName = ResolveUserName(user),
                Summary = "Generated PR Excel",
                Changes = JsonSerializer.Serialize(changes),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write PR Excel audit log for RFQ {RfqId}.", rfqId);
        }
    }

    private sealed class PrLineItem
    {
        public long LineItemId { get; set; }
        public int LineNumber { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Specifications { get; set; }
        public string? Brand { get; set; }
        public string? QuoteBrand { get; set; }
        public double Quantity { get; set; }
        public string? Unit { get; set; }
        public string? LineCurrency { get; set; }
        public long? SelectedQuoteId { get; set; }
        public double? UnitPrice { get; set; }
        public double? TotalPrice { get; set; }
        public string? QuoteCurrency { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierCode { get; set; }
        public string? VendorCode { get; set; }
        public string? ApprovalCompletedAt { get; set; }
    }
}
