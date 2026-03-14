using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rfq")]
[ServiceFilter(typeof(LegacyContractDeprecationFilter))]
public sealed class RfqExportController : NodeControllerBase
{
    private readonly PrExcelService _prExcelService;
    private readonly RfqControllerDataService _rfqControllerDataService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RfqExportController> _logger;

    public RfqExportController(
        PrExcelService prExcelService,
        RfqControllerDataService rfqControllerDataService,
        IWebHostEnvironment environment,
        ILogger<RfqExportController> logger) : base(environment)
    {
        _prExcelService = prExcelService;
        _rfqControllerDataService = rfqControllerDataService;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("{id:long}/generate-pr-excel")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GeneratePrExcel(long id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        if (id <= 0)
        {
            return BadRequest(new { error = "INVALID_RFQ_ID", message = "Invalid RFQ ID provided" });
        }

        if (!JsonHelper.TryGetProperty(body, "lineItemIds", out var lineItemIdsElement) ||
            lineItemIdsElement.ValueKind != JsonValueKind.Array ||
            lineItemIdsElement.GetArrayLength() == 0)
        {
            return BadRequest(new { error = "INVALID_LINE_ITEMS", message = "lineItemIds is required and must be a non-empty array" });
        }

        var lineItemIds = new List<long>();
        var invalidIds = new List<object?>();
        foreach (var entry in lineItemIdsElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetInt64(out var value) || value <= 0)
            {
                invalidIds.Add(ReadInvalidValue(entry));
                continue;
            }

            lineItemIds.Add(value);
        }

        if (invalidIds.Count > 0)
        {
            return BadRequest(new { error = "INVALID_LINE_ITEM_IDS", message = "All lineItemIds must be positive integers", invalidIds });
        }

        const int maxLineItems = 1000;
        if (lineItemIds.Count > maxLineItems)
        {
            return BadRequest(new { error = "TOO_MANY_LINE_ITEMS", message = $"Cannot generate PR Excel with more than {maxLineItems} line items", received = lineItemIds.Count });
        }

        var validationResult = await ValidateLineItemsForExportAsync(id, lineItemIds, cancellationToken);
        var missingQuoteItems = validationResult.InvalidItems.Where(item => string.Equals(item.Reason, "NO_SELECTED_QUOTE", StringComparison.Ordinal)).ToList();

        if (missingQuoteItems.Count > 0)
        {
            return BadRequest(new
            {
                error = "MISSING_SELECTED_QUOTE",
                message = "Some line items do not have a selected quote",
                details = new
                {
                    missingQuoteItems = missingQuoteItems.Select(item => new { id = item.Id, lineNumber = item.LineNumber }),
                },
            });
        }

        var statusInvalidItems = validationResult.InvalidItems.Where(item => !string.Equals(item.Reason, "NO_SELECTED_QUOTE", StringComparison.Ordinal)).ToList();
        if (statusInvalidItems.Count > 0)
        {
            return BadRequest(new
            {
                error = "INVALID_LINE_ITEM_STATUS",
                message = "Some line items are not eligible for export",
                details = new
                {
                    invalidItems = statusInvalidItems.Select(item => new
                    {
                        id = item.Id,
                        lineNumber = item.LineNumber,
                        status = item.Status,
                        quoteStatus = item.QuoteStatus,
                        reason = item.Reason,
                    }),
                },
            });
        }

        if (validationResult.ValidItems.Count == 0)
        {
            return BadRequest(new { error = "NO_VALID_LINE_ITEMS", message = "No valid line items found with approved quotes for the selected items" });
        }

        var prNumber = JsonHelper.GetString(body, "prNumber");
        var department = JsonHelper.GetString(body, "department");
        var accountNo = JsonHelper.GetString(body, "accountNo");

        try
        {
            var user = HttpContext.GetAuthUser();
            var result = await _prExcelService.GenerateAsync(
                id,
                validationResult.ValidItems.Select(item => item.Id).ToList(),
                new PrExcelOptions { PrNumber = prNumber, Department = department, AccountNo = accountNo },
                new PrExcelUserContext { Id = user?.Id ?? string.Empty, Name = user?.Name, Username = user?.Name },
                cancellationToken);

            var buffer = result.Buffer;
            if (buffer == null || buffer.Length == 0)
            {
                return StatusCode(500, new { error = "INVALID_BUFFER", message = "Failed to generate valid Excel file" });
            }

            const int minFileSize = 100 * 1024;
            const int maxFileSize = 50 * 1024 * 1024;

            if (buffer.Length < minFileSize)
            {
                return StatusCode(500, new
                {
                    error = "FILE_TOO_SMALL",
                    message = "Generated file is smaller than expected. Generation may have failed.",
                    fileSizeKB = (buffer.Length / 1024.0).ToString("F2", CultureInfo.InvariantCulture),
                });
            }

            if (buffer.Length > maxFileSize)
            {
                return StatusCode(500, new
                {
                    error = "FILE_TOO_LARGE",
                    message = "Generated file exceeds maximum size limit",
                    fileSizeKB = (buffer.Length / 1024.0).ToString("F2", CultureInfo.InvariantCulture),
                    maxSizeKB = (maxFileSize / 1024.0).ToString("F2", CultureInfo.InvariantCulture),
                });
            }

            var generationTimeMs = (int)(DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var finalPrNumber = string.IsNullOrWhiteSpace(result.PrNumber) ? $"HZ_PR_{timestamp}" : result.PrNumber;
            var filename = $"PR_({finalPrNumber})_{timestamp}.xlsm";

            Response.Headers["Content-Type"] = "application/vnd.ms-excel.sheet.macroEnabled.12";
            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
            Response.Headers["Content-Length"] = buffer.Length.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-File-Size-KB"] = (buffer.Length / 1024.0).ToString("F2", CultureInfo.InvariantCulture);
            Response.Headers["X-Generation-Time-Ms"] = generationTimeMs.ToString(CultureInfo.InvariantCulture);
            Response.Headers["X-Line-Item-Count"] = result.LineItemCount.ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(finalPrNumber))
            {
                var validLineItemIds = validationResult.ValidItems.Select(item => item.Id).ToList();
                await UpdatePrStatusAfterExcelGenerationAsync(id, finalPrNumber, department, user, validLineItemIds, cancellationToken);
            }

            return File(buffer, "application/vnd.ms-excel.sheet.macroEnabled.12");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { error = "RESOURCE_NOT_FOUND", message = ex.Message });
            }

            if (ex.Message.Contains("No line items", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "NO_VALID_LINE_ITEMS", message = "No valid line items found with approved quotes for the selected items" });
            }

            if (ex.Message.Contains("template", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(500, new { error = "TEMPLATE_ERROR", message = "Excel template is missing or corrupted. Please contact system administrator." });
            }

            var details = _environment.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new { error = "GENERATION_FAILED", message = "Failed to generate PR Excel file. Please try again or contact support.", details });
        }
    }

    private static object? ReadInvalidValue(JsonElement entry)
    {
        return entry.ValueKind switch
        {
            JsonValueKind.Number => entry.TryGetInt64(out var longValue)
                ? longValue
                : entry.TryGetDecimal(out var decimalValue)
                    ? decimalValue
                    : entry.GetRawText(),
            JsonValueKind.String => entry.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => entry.GetRawText(),
        };
    }

    private async Task<LineItemExportValidationResult> ValidateLineItemsForExportAsync(long rfqId, IReadOnlyList<long> lineItemIds, CancellationToken cancellationToken)
    {
        var records = await _rfqControllerDataService.LoadLineItemExportRecordsAsync(rfqId, lineItemIds, cancellationToken);
        var recordMap = records.ToDictionary(record => record.Id);
        var validItems = new List<RfqLineItemExportRecord>();
        var invalidItems = new List<RfqLineItemExportRecord>();
        var itemsNeedingFix = new List<RfqLineItemExportRecord>();

        foreach (var id in lineItemIds)
        {
            recordMap.TryGetValue(id, out var record);
            var reason = GetInvalidReason(record);

            if (reason != null && reason.StartsWith("QUOTE_STATUS_", StringComparison.Ordinal) && record?.SelectedQuoteId != null)
            {
                record.Reason = reason;
                itemsNeedingFix.Add(record);
            }
            else if (reason != null)
            {
                var placeholder = record ?? new RfqLineItemExportRecord { Id = id };
                placeholder.Reason = reason;
                invalidItems.Add(placeholder);
            }
            else if (record != null)
            {
                validItems.Add(record);
            }
        }

        if (itemsNeedingFix.Count > 0)
        {
            var now = DateTime.UtcNow.ToString("o");
            foreach (var item in itemsNeedingFix)
            {
                if (!item.SelectedQuoteId.HasValue)
                {
                    continue;
                }

                var quote = await _rfqControllerDataService.FindQuoteAsync(item.SelectedQuoteId.Value, cancellationToken);
                if (quote == null)
                {
                    item.Reason = "QUOTE_STATUS_UNKNOWN";
                    invalidItems.Add(item);
                    continue;
                }

                quote.Status = "selected";
                quote.UpdatedAt = now;
            }

            await _rfqControllerDataService.SaveChangesAsync(cancellationToken);

            foreach (var item in itemsNeedingFix)
            {
                item.QuoteStatus = "selected";
                validItems.Add(item);
            }
        }

        return new LineItemExportValidationResult(validItems, invalidItems);
    }

    private async Task UpdatePrStatusAfterExcelGenerationAsync(
        long rfqId,
        string prNumber,
        string? department,
        AuthUser? user,
        List<long> exportedLineItemIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var existingPr = await _rfqControllerDataService.FindRfqPrRecordAsync(rfqId, cancellationToken);
            if (existingPr != null)
            {
                _logger.LogInformation("PR record already exists for RFQ {RfqId}, skipping status update", rfqId);
                return;
            }

            var rfq = await _rfqControllerDataService.FindRfqAsync(rfqId, cancellationToken);
            if (rfq == null)
            {
                _logger.LogWarning("RFQ {RfqId} not found when updating PR status", rfqId);
                return;
            }

            var now = DateTime.UtcNow.ToString("o");
            var record = new SupplierSystem.Domain.Entities.RfqPrRecord
            {
                RfqId = (int)rfqId,
                PrNumber = prNumber,
                PrDate = now[..10],
                FilledBy = user?.Id ?? "system",
                FilledAt = now,
                DepartmentConfirmerId = null,
                DepartmentConfirmerName = null,
                ConfirmationStatus = "confirmed",
                ConfirmedAt = now,
            };

            await using var transaction = await _rfqControllerDataService.BeginTransactionAsync(cancellationToken);
            _rfqControllerDataService.AddPrRecord(record);

            if (exportedLineItemIds.Count > 0)
            {
                var lineItemsToUpdate = await _rfqControllerDataService.LoadLineItemsAsync(rfqId, exportedLineItemIds, cancellationToken);
                foreach (var lineItem in lineItemsToUpdate)
                {
                    lineItem.Status = "completed";
                    lineItem.UpdatedAt = now;
                }
            }

            rfq.PrStatus = "confirmed";
            await _rfqControllerDataService.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("PR status updated for RFQ {RfqId} with PR number {PrNumber}. {LineItemCount} line items marked as completed.", rfqId, prNumber, exportedLineItemIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update PR status for RFQ {RfqId}. Excel download will continue.", rfqId);
        }
    }

    private static string? GetInvalidReason(RfqLineItemExportRecord? item)
    {
        if (item == null)
        {
            return "NOT_FOUND";
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pending_po", "completed" };
        if (string.IsNullOrWhiteSpace(item.Status) || !allowed.Contains(item.Status))
        {
            return $"STATUS_{item.Status ?? "UNKNOWN"}";
        }

        if (!item.SelectedQuoteId.HasValue)
        {
            return "NO_SELECTED_QUOTE";
        }

        if (!string.Equals(item.QuoteStatus, "selected", StringComparison.OrdinalIgnoreCase))
        {
            return $"QUOTE_STATUS_{item.QuoteStatus ?? "UNKNOWN"}";
        }

        return null;
    }

    private sealed record LineItemExportValidationResult(List<RfqLineItemExportRecord> ValidItems, List<RfqLineItemExportRecord> InvalidItems);
}
