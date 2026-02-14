using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ReconciliationController
{
    private IActionResult? RequireAccess(AuthUser? user, bool allowSupplier, bool allowStaff)
    {
        if (user == null)
        {
            return Unauthorized();
        }

        if (allowStaff && HasAnyPermission(user, StaffPermissions))
        {
            return null;
        }

        if (allowSupplier && user.SupplierId.HasValue && HasAnyPermission(user, SupplierPermissions))
        {
            return null;
        }

        return Forbidden();
    }

    private static bool HasAnyPermission(AuthUser user, params string[] permissions)
    {
        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
    }

    private async Task<List<ReconciliationDto>> BuildReconciliationDtosAsync(
        IEnumerable<Reconciliation> reconciliations,
        CancellationToken cancellationToken)
    {
        var list = reconciliations.ToList();
        if (list.Count == 0)
        {
            return new List<ReconciliationDto>();
        }

        var supplierIds = list.Select(r => r.SupplierId).Distinct().ToList();
        var supplierMap = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.CompanyName, cancellationToken);

        var receiptIds = list.Where(r => r.WarehouseReceiptId.HasValue)
            .Select(r => r.WarehouseReceiptId!.Value)
            .Distinct()
            .ToList();

        var receipts = await _dbContext.WarehouseReceipts.AsNoTracking()
            .Where(r => receiptIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var receiptMap = receipts.ToDictionary(r => r.Id);

        var receiptQuantities = await _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .GroupBy(d => d.WarehouseReceiptId)
            .Select(g => new
            {
                WarehouseReceiptId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineCount = g.Count()
            })
            .ToListAsync(cancellationToken);
        var quantityMap = receiptQuantities
            .Select(entry => new ReceiptQuantityInfo(entry.WarehouseReceiptId, entry.Quantity, entry.LineCount))
            .ToDictionary(entry => entry.WarehouseReceiptId);

        var reconciliationIds = list.Select(r => r.Id).ToList();
        var matches = await _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => reconciliationIds.Contains(m.ReconciliationId))
            .OrderByDescending(m => m.MatchedAt)
            .ToListAsync(cancellationToken);
        var matchMap = matches
            .GroupBy(m => m.ReconciliationId)
            .ToDictionary(g => g.Key, g => g.First());

        var invoiceIds = matches.Select(m => m.InvoiceId).Distinct().ToList();
        var invoices = await _dbContext.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        var invoiceMap = invoices.ToDictionary(i => i.Id);

        var result = new List<ReconciliationDto>();
        foreach (var reconciliation in list)
        {
            supplierMap.TryGetValue(reconciliation.SupplierId, out var supplierName);
            WarehouseReceipt? receipt = null;
            if (reconciliation.WarehouseReceiptId.HasValue)
            {
                receiptMap.TryGetValue(reconciliation.WarehouseReceiptId.Value, out receipt);
            }

            quantityMap.TryGetValue(receipt?.Id ?? 0, out var quantityInfo);
            matchMap.TryGetValue(reconciliation.Id, out var match);
            Invoice? invoice = null;
            if (match != null)
            {
                invoiceMap.TryGetValue(match.InvoiceId, out invoice);
            }

            result.Add(MapReconciliation(reconciliation, supplierName, receipt, quantityInfo, match, invoice));
        }

        return result;
    }

    private static ReconciliationDto MapReconciliation(
        Reconciliation reconciliation,
        string? supplierName,
        WarehouseReceipt? receipt,
        ReceiptQuantityInfo? quantityInfo,
        InvoiceReconciliationMatch? match,
        Invoice? invoice)
    {
        var totalAmount = receipt?.TotalAmount ?? reconciliation.TotalReceiptAmount ?? reconciliation.TotalInvoiceAmount ?? 0;
        var varianceAmount = reconciliation.VarianceAmount ??
                             (invoice != null && receipt != null ? invoice.Amount - receipt.TotalAmount : 0);
        var warehouseOrderNo = receipt?.ReceiptNumber ??
                               reconciliation.ReconciliationNumber ??
                               $"REC-{reconciliation.Id}";
        var receivedDate = receipt?.ReceiptDate ?? reconciliation.PeriodStart ?? reconciliation.CreatedAt;
        var totalQuantity = quantityInfo?.Quantity ?? 0m;

        var confirmed = string.Equals(reconciliation.Status, ReconciliationStateMachine.Statuses.Confirmed, StringComparison.OrdinalIgnoreCase)
                        || !string.IsNullOrWhiteSpace(reconciliation.ConfirmedAt);

        return new ReconciliationDto
        {
            Id = reconciliation.Id,
            WarehouseOrderNo = warehouseOrderNo,
            SupplierId = reconciliation.SupplierId,
            InvoiceId = match?.InvoiceId,
            ReceivedDate = receivedDate,
            TotalAmount = totalAmount,
            TotalQuantity = totalQuantity,
            Status = reconciliation.Status ?? ReconciliationStateMachine.Statuses.Pending,
            VarianceAmount = varianceAmount,
            AccountantConfirmed = confirmed,
            AccountantId = reconciliation.ConfirmedBy,
            AccountantNotes = reconciliation.Notes,
            ConfirmedAt = reconciliation.ConfirmedAt,
            CreatedAt = reconciliation.CreatedAt,
            UpdatedAt = reconciliation.UpdatedAt,
            SupplierName = supplierName,
            InvoiceNumber = invoice?.InvoiceNumber,
            InvoiceDate = invoice?.InvoiceDate.ToString("yyyy-MM-dd"),
            InvoiceAmount = invoice?.Amount
        };
    }

    private async Task<(decimal acceptable, decimal warning)> GetVarianceThresholdsAsync(CancellationToken cancellationToken)
    {
        var threshold = await _dbContext.ReconciliationThresholds.AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var acceptable = threshold?.AcceptableVariancePercentage ?? 5;
        var warning = threshold?.WarningVariancePercentage ?? 10;
        return (acceptable, warning);
    }

    private static bool TryParseDate(string? value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTime.TryParse(value, out parsed);
    }

    private sealed class ReconciliationDto
    {
        public int Id { get; set; }
        public string? WarehouseOrderNo { get; set; }

        [JsonPropertyName("supplier_id")]
        public int SupplierId { get; set; }

        [JsonPropertyName("invoice_id")]
        public int? InvoiceId { get; set; }

        [JsonPropertyName("received_date")]
        public string? ReceivedDate { get; set; }

        public decimal TotalAmount { get; set; }

        [JsonPropertyName("total_quantity")]
        public decimal TotalQuantity { get; set; }

        public string? Status { get; set; }

        [JsonPropertyName("variance_amount")]
        public decimal VarianceAmount { get; set; }

        [JsonPropertyName("accountant_confirmed")]
        public bool AccountantConfirmed { get; set; }

        [JsonPropertyName("accountant_id")]
        public int? AccountantId { get; set; }

        [JsonPropertyName("accountant_notes")]
        public string? AccountantNotes { get; set; }

        [JsonPropertyName("confirmed_at")]
        public string? ConfirmedAt { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public string? UpdatedAt { get; set; }

        public string? SupplierName { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? InvoiceDate { get; set; }
        public decimal? InvoiceAmount { get; set; }

        public VarianceAnalysisDto? VarianceAnalysis { get; set; }
        public List<StatusHistoryDto>? StatusHistory { get; set; }
        public List<InvoiceMatchDto>? InvoiceMatches { get; set; }
    }

    private sealed class VarianceAnalysisDto
    {
        [JsonPropertyName("variance_amount")]
        public decimal VarianceAmount { get; set; }

        [JsonPropertyName("variance_percentage")]
        public decimal? VariancePercentage { get; set; }

        [JsonPropertyName("variance_reason")]
        public string? VarianceReason { get; set; }
    }

    private sealed class StatusHistoryDto
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("changed_by")]
        public int? ChangedBy { get; set; }

        [JsonPropertyName("change_reason")]
        public string? ChangeReason { get; set; }

        [JsonPropertyName("changed_at")]
        public string? ChangedAt { get; set; }
    }

    private sealed class InvoiceMatchDto
    {
        public int Id { get; set; }

        [JsonPropertyName("invoice_id")]
        public int InvoiceId { get; set; }

        [JsonPropertyName("reconciliation_id")]
        public int ReconciliationId { get; set; }

        [JsonPropertyName("matched_amount")]
        public decimal MatchedAmount { get; set; }

        [JsonPropertyName("variance_amount")]
        public decimal? VarianceAmount { get; set; }

        [JsonPropertyName("variance_percentage")]
        public decimal? VariancePercentage { get; set; }

        [JsonPropertyName("matching_status")]
        public string? MatchingStatus { get; set; }
    }

    private sealed class ReconciliationStatsDto
    {
        [JsonPropertyName("total_reconciliations")]
        public int TotalReconciliations { get; set; }

        [JsonPropertyName("matched_count")]
        public int MatchedCount { get; set; }

        [JsonPropertyName("variance_count")]
        public int VarianceCount { get; set; }

        [JsonPropertyName("unmatched_count")]
        public int UnmatchedCount { get; set; }

        [JsonPropertyName("confirmed_count")]
        public int ConfirmedCount { get; set; }

        [JsonPropertyName("total_variance_amount")]
        public decimal TotalVarianceAmount { get; set; }

        [JsonPropertyName("avg_variance_amount")]
        public decimal AvgVarianceAmount { get; set; }
    }

    private sealed class SupplierStatsDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;

        [JsonPropertyName("reconciliation_count")]
        public int ReconciliationCount { get; set; }

        [JsonPropertyName("matched_count")]
        public int MatchedCount { get; set; }

        [JsonPropertyName("variance_count")]
        public int VarianceCount { get; set; }

        [JsonPropertyName("total_reconciliation_amount")]
        public decimal TotalReconciliationAmount { get; set; }

        [JsonPropertyName("avg_variance")]
        public decimal AvgVariance { get; set; }
    }

    private sealed class VarianceTrendDto
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }

        [JsonPropertyName("avg_variance")]
        public decimal AvgVariance { get; set; }

        [JsonPropertyName("positive_variance_count")]
        public int PositiveVarianceCount { get; set; }

        [JsonPropertyName("negative_variance_count")]
        public int NegativeVarianceCount { get; set; }
    }

    private sealed class WarehouseReceiptSummaryItem
    {
        public int Id { get; set; }

        [JsonPropertyName("reconciliation_id")]
        public int? ReconciliationId { get; set; }

        [JsonPropertyName("receipt_number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [JsonPropertyName("receipt_date")]
        public string ReceiptDate { get; set; } = string.Empty;

        [JsonPropertyName("warehouse_location")]
        public string? WarehouseLocation { get; set; }

        [JsonPropertyName("received_by")]
        public string? ReceivedBy { get; set; }

        [JsonPropertyName("item_details")]
        public string? ItemDetails { get; set; }

        [JsonPropertyName("total_items")]
        public decimal TotalItems { get; set; }
    }

    private sealed class WarehouseReceiptSummaryResponse
    {
        public List<WarehouseReceiptSummaryItem> WarehouseReceipts { get; set; } = new();
        public WarehouseReceiptSummaryTotals Summary { get; set; } = new();
    }

    private sealed class WarehouseReceiptSummaryTotals
    {
        [JsonPropertyName("total_receipts")]
        public int TotalReceipts { get; set; }

        [JsonPropertyName("total_quantity")]
        public decimal TotalQuantity { get; set; }

        [JsonPropertyName("matched_receipts")]
        public int MatchedReceipts { get; set; }

        [JsonPropertyName("variance_receipts")]
        public int VarianceReceipts { get; set; }
    }

    private sealed record ReceiptQuantityInfo(int WarehouseReceiptId, decimal Quantity, int LineCount);
}
