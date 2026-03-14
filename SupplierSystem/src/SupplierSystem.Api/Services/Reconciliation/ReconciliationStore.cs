using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Reconciliation;

public sealed class ReconciliationStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public ReconciliationStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<SupplierSystem.Domain.Entities.Reconciliation> QueryReconciliations()
    {
        return _dbContext.Reconciliations.AsNoTracking();
    }

    public Task<SupplierSystem.Domain.Entities.Reconciliation?> FindReconciliationAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Reconciliations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<SupplierSystem.Domain.Entities.Reconciliation?> FindReconciliationForUpdateAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<Dictionary<int, string?>> LoadSupplierNameMapAsync(IReadOnlyCollection<int> supplierIds, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (string?)s.CompanyName, cancellationToken);
    }

    public Task<List<WarehouseReceipt>> LoadWarehouseReceiptsAsync(IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceipts.AsNoTracking()
            .Where(r => receiptIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ReceiptQuantityInfoRow>> LoadReceiptQuantityInfosAsync(IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .GroupBy(d => d.WarehouseReceiptId)
            .Select(g => new ReceiptQuantityInfoRow
            {
                WarehouseReceiptId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineCount = g.Count(),
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<InvoiceReconciliationMatch>> LoadMatchesForReconciliationsAsync(IReadOnlyCollection<int> reconciliationIds, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => reconciliationIds.Contains(m.ReconciliationId))
            .OrderByDescending(m => m.MatchedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Invoice>> LoadInvoicesAsync(IReadOnlyCollection<int> invoiceIds, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasDuplicateInvoiceNumberAsync(int supplierId, string invoiceNumber, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices.AsNoTracking()
            .AnyAsync(i => i.SupplierId == supplierId && i.InvoiceNumber == invoiceNumber, cancellationToken);
    }

    public void AddInvoice(Invoice invoice)
    {
        _dbContext.Invoices.Add(invoice);
    }

    public Task<Invoice?> FindInvoiceForUpdateAsync(int invoiceId, CancellationToken cancellationToken)
    {
        return _dbContext.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
    }

    public void AddInvoiceFile(InvoiceFile invoiceFile)
    {
        _dbContext.InvoiceFiles.Add(invoiceFile);
    }

    public Task<ReconciliationThreshold?> LoadLatestThresholdAsync(CancellationToken cancellationToken)
    {
        return _dbContext.ReconciliationThresholds.AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<WarehouseReceipt>> LoadWarehouseReceiptsBySupplierAsync(int? supplierId, CancellationToken cancellationToken)
    {
        var query = _dbContext.WarehouseReceipts.AsNoTracking();
        if (supplierId.HasValue)
        {
            query = query.Where(r => r.SupplierId == supplierId.Value);
        }

        return query.ToListAsync(cancellationToken);
    }

    public Task<WarehouseReceipt?> FindWarehouseReceiptAsync(int receiptId, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceipts.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);
    }

    public Task<int?> LoadWarehouseReceiptIdForReconciliationAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.Reconciliations.AsNoTracking()
            .Where(rec => rec.Id == reconciliationId)
            .Select(rec => rec.WarehouseReceiptId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<ReceiptAggregateRow>> LoadReceiptAggregatesAsync(IReadOnlyCollection<int> receiptIds, CancellationToken cancellationToken)
    {
        return _dbContext.WarehouseReceiptDetails.AsNoTracking()
            .Where(d => receiptIds.Contains(d.WarehouseReceiptId))
            .GroupBy(d => d.WarehouseReceiptId)
            .Select(g => new ReceiptAggregateRow
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

    public Task<List<ReconciliationVarianceAnalysis>> LoadVarianceAnalysesAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.ReconciliationVarianceAnalyses.AsNoTracking()
            .Where(v => v.ReconciliationId == reconciliationId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ReconciliationStatusHistory>> LoadStatusHistoriesAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.ReconciliationStatusHistories.AsNoTracking()
            .Where(h => h.ReconciliationId == reconciliationId)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<InvoiceReconciliationMatch>> LoadMatchesAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceReconciliationMatches.AsNoTracking()
            .Where(m => m.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);
    }

    public void AddVarianceAnalysis(ReconciliationVarianceAnalysis analysis)
    {
        _dbContext.ReconciliationVarianceAnalyses.Add(analysis);
    }

    public Task<InvoiceReconciliationMatch?> FindMatchForUpdateAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceReconciliationMatches
            .FirstOrDefaultAsync(m => m.ReconciliationId == reconciliationId, cancellationToken);
    }

    public void AddMatch(InvoiceReconciliationMatch match)
    {
        _dbContext.InvoiceReconciliationMatches.Add(match);
    }

    public void AddStatusHistory(ReconciliationStatusHistory history)
    {
        _dbContext.ReconciliationStatusHistories.Add(history);
    }

    public Task<List<InvoiceReconciliationMatch>> LoadMatchesForDeleteAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceReconciliationMatches
            .Where(m => m.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ReconciliationVarianceAnalysis>> LoadAnalysesForDeleteAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.ReconciliationVarianceAnalyses
            .Where(v => v.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<ReconciliationStatusHistory>> LoadHistoriesForDeleteAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.ReconciliationStatusHistories
            .Where(h => h.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);
    }

    public Task<List<InvoiceFile>> LoadInvoiceFilesForDeleteAsync(int reconciliationId, CancellationToken cancellationToken)
    {
        return _dbContext.InvoiceFiles
            .Where(f => f.ReconciliationId == reconciliationId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveMatches(IEnumerable<InvoiceReconciliationMatch> matches)
    {
        _dbContext.InvoiceReconciliationMatches.RemoveRange(matches);
    }

    public void RemoveAnalyses(IEnumerable<ReconciliationVarianceAnalysis> analyses)
    {
        _dbContext.ReconciliationVarianceAnalyses.RemoveRange(analyses);
    }

    public void RemoveHistories(IEnumerable<ReconciliationStatusHistory> histories)
    {
        _dbContext.ReconciliationStatusHistories.RemoveRange(histories);
    }

    public void RemoveInvoiceFiles(IEnumerable<InvoiceFile> invoiceFiles)
    {
        _dbContext.InvoiceFiles.RemoveRange(invoiceFiles);
    }

    public void RemoveReconciliation(SupplierSystem.Domain.Entities.Reconciliation reconciliation)
    {
        _dbContext.Reconciliations.Remove(reconciliation);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }
}

public sealed class ReceiptQuantityInfoRow
{
    public int WarehouseReceiptId { get; init; }
    public decimal Quantity { get; init; }
    public int LineCount { get; init; }
}

public sealed class ReceiptAggregateRow
{
    public int WarehouseReceiptId { get; init; }
    public decimal Quantity { get; init; }
    public int LineCount { get; init; }
}
