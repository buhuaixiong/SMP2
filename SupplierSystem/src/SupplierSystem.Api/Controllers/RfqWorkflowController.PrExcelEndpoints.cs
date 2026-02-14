using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
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
            return BadRequest(new
            {
                error = "INVALID_RFQ_ID",
                message = "Invalid RFQ ID provided",
            });
        }

        if (!JsonHelper.TryGetProperty(body, "lineItemIds", out var lineItemIdsElement) ||
            lineItemIdsElement.ValueKind != JsonValueKind.Array ||
            lineItemIdsElement.GetArrayLength() == 0)
        {
            return BadRequest(new
            {
                error = "INVALID_LINE_ITEMS",
                message = "lineItemIds is required and must be a non-empty array",
            });
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
            return BadRequest(new
            {
                error = "INVALID_LINE_ITEM_IDS",
                message = "All lineItemIds must be positive integers",
                invalidIds,
            });
        }

        const int maxLineItems = 1000;
        if (lineItemIds.Count > maxLineItems)
        {
            return BadRequest(new
            {
                error = "TOO_MANY_LINE_ITEMS",
                message = $"Cannot generate PR Excel with more than {maxLineItems} line items",
                received = lineItemIds.Count,
            });
        }

        var validationResult = await ValidateLineItemsForExportAsync(id, lineItemIds, cancellationToken);

        var missingQuoteItems = validationResult.InvalidItems
            .Where(item => string.Equals(item.Reason, "NO_SELECTED_QUOTE", StringComparison.Ordinal))
            .ToList();

        if (missingQuoteItems.Count > 0)
        {
            return BadRequest(new
            {
                error = "MISSING_SELECTED_QUOTE",
                message = "Some line items do not have a selected quote",
                details = new
                {
                    missingQuoteItems = missingQuoteItems.Select(item => new
                    {
                        id = item.Id,
                        lineNumber = item.LineNumber,
                    }),
                },
            });
        }

        var statusInvalidItems = validationResult.InvalidItems
            .Where(item => !string.Equals(item.Reason, "NO_SELECTED_QUOTE", StringComparison.Ordinal))
            .ToList();

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
            return BadRequest(new
            {
                error = "NO_VALID_LINE_ITEMS",
                message = "No valid line items found with approved quotes for the selected items",
            });
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
                new SupplierSystem.Api.Services.PrExcelOptions
                {
                    PrNumber = prNumber,
                    Department = department,
                    AccountNo = accountNo,
                },
                new SupplierSystem.Api.Services.PrExcelUserContext
                {
                    Id = user?.Id ?? string.Empty,
                    Name = user?.Name,
                    Username = user?.Name,
                },
                cancellationToken);

            var buffer = result.Buffer;
            if (buffer == null || buffer.Length == 0)
            {
                return StatusCode(500, new
                {
                    error = "INVALID_BUFFER",
                    message = "Failed to generate valid Excel file",
                });
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
            var finalPrNumber = string.IsNullOrWhiteSpace(result.PrNumber)
                ? $"HZ_PR_{timestamp}"
                : result.PrNumber;
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

            var rfqForNotification = await _dbContext.Rfqs.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfqForNotification != null)
            {
                await TryNotifyPrExportedAsync(rfqForNotification, cancellationToken);
            }

            return File(buffer, "application/vnd.ms-excel.sheet.macroEnabled.12");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new
                {
                    error = "RESOURCE_NOT_FOUND",
                    message = ex.Message,
                });
            }

            if (ex.Message.Contains("No line items", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    error = "NO_VALID_LINE_ITEMS",
                    message = "No valid line items found with approved quotes for the selected items",
                });
            }

            if (ex.Message.Contains("template", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(500, new
                {
                    error = "TEMPLATE_ERROR",
                    message = "Excel template is missing or corrupted. Please contact system administrator.",
                });
            }

            var details = _environment.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new
            {
                error = "GENERATION_FAILED",
                message = "Failed to generate PR Excel file. Please try again or contact support.",
                details,
            });
        }
    }

    private async Task<LineItemExportValidationResult> ValidateLineItemsForExportAsync(
        long rfqId,
        IReadOnlyList<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        var records = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                             join q in _dbContext.Quotes.AsNoTracking()
                                 on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                             from q in quoteGroup.DefaultIfEmpty()
                             where li.RfqId == rfqId && lineItemIds.Contains(li.Id)
                             select new LineItemExportRecord
                             {
                                 Id = li.Id,
                                 LineNumber = li.LineNumber,
                                 Status = li.Status,
                                 SelectedQuoteId = li.SelectedQuoteId,
                                 QuoteStatus = q != null ? q.Status : null,
                             })
            .ToListAsync(cancellationToken);

        var recordMap = records.ToDictionary(record => record.Id);
        var validItems = new List<LineItemExportRecord>();
        var invalidItems = new List<LineItemExportRecord>();
        var itemsNeedingFix = new List<LineItemExportRecord>();

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
                var placeholder = record ?? new LineItemExportRecord { Id = id };
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

                var quote = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == item.SelectedQuoteId.Value, cancellationToken);
                if (quote == null)
                {
                    item.Reason = "QUOTE_STATUS_UNKNOWN";
                    invalidItems.Add(item);
                    continue;
                }

                quote.Status = "selected";
                quote.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

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
            var existingPr = await _dbContext.RfqPrRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.RfqId == rfqId, cancellationToken);

            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
            if (rfq == null)
            {
                _logger.LogWarning("RFQ {RfqId} not found when updating PR status", rfqId);
                return;
            }

            var now = DateTime.UtcNow.ToString("o");

            if (existingPr != null)
            {
                // PR记录已存在，检查是否是同一个PR单号
                if (string.Equals(existingPr.PrNumber, prNumber, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("PR record already exists for RFQ {RfqId} with same PR number {PrNumber}, checking for new line items to update",
                        rfqId, prNumber);
                    // 同一个PR单号，检查是否有新的行项目需要更新状态
                    var completedLineItemIds = await _dbContext.RfqLineItems
                        .AsNoTracking()
                        .Where(li => li.RfqId == rfqId && li.Status == "completed")
                        .Select(li => li.Id)
                        .ToListAsync(cancellationToken);

                    var newLineItemIds = exportedLineItemIds.Where(id => !completedLineItemIds.Contains(id)).ToList();

                    if (newLineItemIds.Count == 0)
                    {
                        _logger.LogInformation("No new line items to update for RFQ {RfqId}", rfqId);
                        await _priceAuditService.UpdatePrExportAsync(
                            rfqId,
                            exportedLineItemIds,
                            user?.Id ?? "system",
                            now,
                            cancellationToken);
                        return;
                    }

                    // 更新新行项目的状态
                    var lineItemsToUpdate = await _dbContext.RfqLineItems
                        .Where(li => li.RfqId == rfqId && newLineItemIds.Contains(li.Id))
                        .ToListAsync(cancellationToken);

                    foreach (var lineItem in lineItemsToUpdate)
                    {
                        lineItem.Status = "completed";
                        lineItem.UpdatedAt = now;
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await _priceAuditService.UpdatePrExportAsync(
                        rfqId,
                        newLineItemIds,
                        user?.Id ?? "system",
                        now,
                        cancellationToken);
                    _logger.LogInformation("Updated {Count} new line items to completed for RFQ {RfqId}", newLineItemIds.Count, rfqId);
                    return;
                }
                else
                {
                    // 不同PR单号，允许创建新记录（记录多次PR情况）
                    _logger.LogInformation("Different PR number detected for RFQ {RfqId}. Existing: {ExistingPrNumber}, New: {NewPrNumber}. Creating new record.",
                        rfqId, existingPr.PrNumber, prNumber);
                }
            }
            else
            {
                _logger.LogInformation("No existing PR record for RFQ {RfqId}, creating new record", rfqId);
            }

            // 创建新的PR记录
            var record = new RfqPrRecord
            {
                RfqId = (int)rfqId,
                PrNumber = prNumber,
                PrDate = now.Substring(0, 10),
                FilledBy = user?.Id ?? "system",
                FilledAt = now,
                DepartmentConfirmerId = null,
                DepartmentConfirmerName = null,
                ConfirmationStatus = "confirmed",
                ConfirmedAt = now,
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            _dbContext.RfqPrRecords.Add(record);

            if (exportedLineItemIds.Count > 0)
            {
                var lineItemsToUpdate = await _dbContext.RfqLineItems
                    .Where(li => li.RfqId == rfqId && exportedLineItemIds.Contains(li.Id))
                    .ToListAsync(cancellationToken);

                foreach (var lineItem in lineItemsToUpdate)
                {
                    lineItem.Status = "completed";
                    lineItem.UpdatedAt = now;
                }
            }

            rfq.PrStatus = "confirmed";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _priceAuditService.UpdatePrExportAsync(
                rfqId,
                exportedLineItemIds,
                user?.Id ?? "system",
                now,
                cancellationToken);

            _logger.LogInformation("PR status updated for RFQ {RfqId} with PR number {PrNumber}. {LineItemCount} line items marked as completed.",
                rfqId, prNumber, exportedLineItemIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update PR status for RFQ {RfqId}. Excel download will continue.", rfqId);
        }
    }

    private static string? GetInvalidReason(LineItemExportRecord? item)
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

    private sealed class LineItemExportRecord
    {
        public long Id { get; set; }
        public int? LineNumber { get; set; }
        public string? Status { get; set; }
        public long? SelectedQuoteId { get; set; }
        public string? QuoteStatus { get; set; }
        public string? Reason { get; set; }
    }

    private sealed record LineItemExportValidationResult(
        List<LineItemExportRecord> ValidItems,
        List<LineItemExportRecord> InvalidItems);
}
