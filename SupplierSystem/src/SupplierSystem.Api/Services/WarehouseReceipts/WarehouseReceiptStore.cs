using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.WarehouseReceipts;

public sealed class WarehouseReceiptStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public WarehouseReceiptStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<WarehouseReceipt> QueryWarehouseReceipts()
    {
        return _dbContext.WarehouseReceipts.AsNoTracking();
    }

    public Task<WarehouseReceipt?> FindWarehouseReceiptAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<List<WarehouseReceiptDetail>> LoadReceiptDetailsAsync(IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ReceiptLineSummaryRow>> LoadReceiptLineSummariesAsync(IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .GroupBy(d => d.WarehouseReceiptId)
            .Select(g => new ReceiptLineSummaryRow
            {
                WarehouseReceiptId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineCount = g.Count(),
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<SupplierSystem.Domain.Entities.Reconciliation>> LoadReconciliationsByReceiptIdsAsync(IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken)
    {
        return _dbContext.Reconciliations.AsNoTracking()
            .Where(r => r.WarehouseReceiptId.HasValue && receiptIds.Contains(r.WarehouseReceiptId.Value))
            .ToListAsync(cancellationToken);
    }

    public Task<SupplierSystem.Domain.Entities.Reconciliation?> FindReconciliationByReceiptIdAsync(int receiptId, CancellationToken cancellationToken)
    {
        return _dbContext.Reconciliations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.WarehouseReceiptId == receiptId, cancellationToken);
    }

    public Task<List<InvoiceReconciliationMatch>> LoadMatchesByReconciliationIdsAsync(IReadOnlyCollection<int> reconciliationIds, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => reconciliationIds.Contains(m.ReconciliationId))
            .OrderByDescending(m => m.MatchedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<InvoiceReconciliationMatch?> FindLatestMatchAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => m.ReconciliationId == reconciliationId)
            .OrderByDescending(m => m.MatchedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Dictionary<int, string?>> LoadInvoiceNumbersAsync(IReadOnlyCollection<int> invoiceIds, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => (string?)i.InvoiceNumber, cancellationToken);
    }

    public Task<string?> FindInvoiceNumberAsync(int invoiceId, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices.AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Dictionary<int, string?>> LoadSupplierNamesAsync(IReadOnlyCollection<int> supplierIds, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (string?)s.CompanyName, cancellationToken);
    }

    public Task<string?> FindSupplierNameAsync(int supplierId, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.Id == supplierId)
            .Select(s => s.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed class ReceiptLineSummaryRow
{
    public int WarehouseReceiptId { get; init; }
    public decimal Quantity { get; init; }
    public int LineCount { get; init; }
}
