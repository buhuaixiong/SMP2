using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class WarehouseReceiptsController
{
    [HttpGet]
    public async Task<IActionResult> ListWarehouseReceipts(
        [FromQuery] int? supplierId,
        [FromQuery] string? receiptNumber,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (user?.SupplierId.HasValue == true && !HasAnyPermission(user, StaffPermissions))
        {
            supplierId = user.SupplierId;
        }

        var query = _dbContext.WarehouseReceipts.AsNoTracking();

        if (supplierId.HasValue)
        {
            query = query.Where(r => r.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(receiptNumber))
        {
            var keyword = receiptNumber.Trim();
            query = query.Where(r => EF.Functions.Like(r.ReceiptNumber, $"%{keyword}%"));
        }

        var receipts = await query.ToListAsync(cancellationToken);

        if (TryParseDate(startDate, out var start) || TryParseDate(endDate, out var end))
        {
            receipts = receipts.Where(receipt =>
            {
                if (!TryParseDate(receipt.ReceiptDate, out var date))
                {
                    return false;
                }

                if (TryParseDate(startDate, out var s) && date < s)
                {
                    return false;
                }

                if (TryParseDate(endDate, out var e) && date > e)
                {
                    return false;
                }

                return true;
            }).ToList();
        }

        var receiptIds = receipts.Select(r => r.Id).ToList();
        var detailLines = await _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .GroupBy(d => d.WarehouseReceiptId)
            .Select(g => new ReceiptLineSummary(g.Key, g.Sum(x => x.Quantity), g.Count()))
            .ToListAsync(cancellationToken);
        var detailMap = detailLines.ToDictionary(d => d.WarehouseReceiptId);

        var reconciliations = await _dbContext.Reconciliations.AsNoTracking()
            .Where(r => r.WarehouseReceiptId.HasValue && receiptIds.Contains(r.WarehouseReceiptId.Value))
            .ToListAsync(cancellationToken);
        var reconciliationMap = reconciliations.ToDictionary(r => r.WarehouseReceiptId!.Value);

        var reconciliationIds = reconciliations.Select(r => r.Id).ToList();
        var matches = await _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => reconciliationIds.Contains(m.ReconciliationId))
            .OrderByDescending(m => m.MatchedAt)
            .ToListAsync(cancellationToken);
        var matchMap = matches.GroupBy(m => m.ReconciliationId).ToDictionary(g => g.Key, g => g.First());

        var invoiceIds = matchMap.Values.Select(m => m.InvoiceId).Distinct().ToList();
        var invoiceMap = await _dbContext.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.InvoiceNumber, cancellationToken);

        var supplierIds = receipts.Select(r => r.SupplierId).Distinct().ToList();
        var supplierMap = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.CompanyName, cancellationToken);

        var items = receipts
            .OrderByDescending(r => TryParseDate(r.ReceiptDate, out var parsed) ? parsed : DateTime.MinValue)
            .Select(receipt =>
            {
                reconciliationMap.TryGetValue(receipt.Id, out var reconciliation);
                detailMap.TryGetValue(receipt.Id, out var detailInfo);
                supplierMap.TryGetValue(receipt.SupplierId, out var supplierName);

                string? invoiceNumber = null;
                if (reconciliation != null && matchMap.TryGetValue(reconciliation.Id, out var match))
                {
                    invoiceMap.TryGetValue(match.InvoiceId, out invoiceNumber);
                }

                return new WarehouseReceiptSummaryDto
                {
                    Id = receipt.Id,
                    ReconciliationId = reconciliation?.Id,
                    ReceiptNumber = receipt.ReceiptNumber,
                    ReceiptDate = receipt.ReceiptDate,
                    WarehouseLocation = receipt.WarehouseLocation,
                    ReceivedBy = receipt.ReceiverName,
                    ItemDetails = null,
                    TotalItems = detailInfo?.LineCount ?? 0,
                    ReconciliationNumber = reconciliation?.ReconciliationNumber,
                    SupplierName = supplierName,
                    InvoiceNumber = invoiceNumber
                };
            })
            .ToList();

        return Success(items);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetWarehouseReceipt(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var receipt = await _dbContext.WarehouseReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (receipt == null)
        {
            return NotFound("Warehouse receipt not found.");
        }

        if (user?.SupplierId.HasValue == true && !HasAnyPermission(user, StaffPermissions) &&
            receipt.SupplierId != user.SupplierId.Value)
        {
            return Forbidden();
        }

        var detailLines = await _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => d.WarehouseReceiptId == id)
            .ToListAsync(cancellationToken);

        var reconciliation = await _dbContext.Reconciliations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.WarehouseReceiptId == id, cancellationToken);

        string? invoiceNumber = null;
        if (reconciliation != null)
        {
            var match = await _dbContext.InvoiceReconciliationMatches.AsNoTracking()
                .Where(m => m.ReconciliationId == reconciliation.Id)
                .OrderByDescending(m => m.MatchedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (match != null)
            {
                invoiceNumber = await _dbContext.Invoices.AsNoTracking()
                    .Where(i => i.Id == match.InvoiceId)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        var supplierName = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.Id == receipt.SupplierId)
            .Select(s => s.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);

        var summary = new WarehouseReceiptSummaryDto
        {
            Id = receipt.Id,
            ReconciliationId = reconciliation?.Id,
            ReceiptNumber = receipt.ReceiptNumber,
            ReceiptDate = receipt.ReceiptDate,
            WarehouseLocation = receipt.WarehouseLocation,
            ReceivedBy = receipt.ReceiverName,
            ItemDetails = null,
            TotalItems = detailLines.Count,
            ReconciliationNumber = reconciliation?.ReconciliationNumber,
            SupplierName = supplierName,
            InvoiceNumber = invoiceNumber
        };

        var response = new WarehouseReceiptDetailResponse
        {
            Receipt = summary,
            LineItems = detailLines.Select(detail => new WarehouseReceiptLineItemDto
            {
                Id = detail.Id,
                ItemCode = detail.ItemCode,
                ItemName = detail.ItemName,
                Specification = detail.Specification,
                Unit = detail.Unit,
                Quantity = detail.Quantity,
                UnitPrice = detail.UnitPrice,
                Amount = detail.Amount,
                TaxRate = detail.TaxRate,
                TaxAmount = detail.TaxAmount,
                TotalAmount = detail.TotalAmount
            }).ToList()
        };

        return Success(response);
    }
}
