using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Settlements;

public sealed class SettlementStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public SettlementStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<SettlementListItem> QuerySettlementListItems()
    {
        return from settlement in _dbContext.Settlements.AsNoTracking()
               join supplier in _dbContext.Suppliers.AsNoTracking()
                   on settlement.SupplierId equals supplier.Id into supplierGroup
               from supplier in supplierGroup.DefaultIfEmpty()
               join rfq in _dbContext.Rfqs.AsNoTracking()
                   on (long?)settlement.RfqId equals rfq.Id into rfqGroup
               from rfq in rfqGroup.DefaultIfEmpty()
               join creator in _dbContext.Users.AsNoTracking()
                   on settlement.CreatedBy equals creator.Id into creatorGroup
               from creator in creatorGroup.DefaultIfEmpty()
               select new SettlementListItem
               {
                   Settlement = settlement,
                   SupplierName = supplier.CompanyName,
                   SupplierStage = supplier.Stage,
                   CreatorName = creator.Name,
                   RfqTitle = rfq.Title,
               };
    }

    public Task<List<SettlementStatementSupplier>> LoadEligibleSuppliersAsync(
        IReadOnlyCollection<int> supplierIds,
        CancellationToken cancellationToken)
    {
        var suppliersQuery = _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.Stage == "formal" && s.Status == "active");

        if (supplierIds.Count > 0)
        {
            suppliersQuery = suppliersQuery.Where(s => supplierIds.Contains(s.Id));
        }

        return suppliersQuery
            .Select(s => new SettlementStatementSupplier
            {
                Id = s.Id,
                CompanyName = s.CompanyName,
                BankAccount = s.BankAccount,
                ContactEmail = s.ContactEmail,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<List<SettlementStatementInvoice>> LoadVerifiedInvoicesAsync(
        int supplierId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        return (from invoice in _dbContext.Invoices.AsNoTracking()
                join rfq in _dbContext.Rfqs.AsNoTracking()
                    on (long?)invoice.RfqId equals rfq.Id into rfqGroup
                from rfq in rfqGroup.DefaultIfEmpty()
                where invoice.SupplierId == supplierId
                      && invoice.Status == "verified"
                      && invoice.InvoiceDate >= startDate
                      && invoice.InvoiceDate < endDate
                orderby invoice.InvoiceDate
                select new SettlementStatementInvoice
                {
                    Id = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    Amount = invoice.Amount,
                    TaxRate = invoice.TaxRate,
                    InvoiceDate = invoice.InvoiceDate,
                    RfqId = invoice.RfqId,
                    RfqTitle = rfq.Title,
                })
            .ToListAsync(cancellationToken);
    }

    public Task<Settlement?> FindSettlementByIdAndTypeAsync(int id, string type, CancellationToken cancellationToken)
    {
        return _dbContext.Settlements
            .FirstOrDefaultAsync(s => s.Id == id && s.Type == type, cancellationToken);
    }

    public Task<Settlement?> FindDisputableSettlementAsync(int id, CancellationToken cancellationToken)
    {
        return _dbContext.Settlements
            .FirstOrDefaultAsync(s => s.Id == id && (s.Type == "monthly" || s.Type == "quarterly"), cancellationToken);
    }

    public Task<Supplier?> FindSupplierAsync(int supplierId, CancellationToken cancellationToken)
    {
        return _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
    }

    public Task<decimal?> FindRfqAmountAsync(int rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.Rfqs.AsNoTracking()
            .Where(r => r.Id == rfqId)
            .Select(r => r.Amount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountApprovedOverdueSettlementsAsync(int supplierId, string nowText, CancellationToken cancellationToken)
    {
        return _dbContext.Settlements.AsNoTracking()
            .Where(s => s.SupplierId == supplierId
                        && s.Status == "approved"
                        && s.PaymentDueDate != null
                        && string.Compare(s.PaymentDueDate, nowText) < 0)
            .CountAsync(cancellationToken);
    }

    public async Task<SettlementProgressTracking> GetProgressTrackingAsync(
        DateTime threshold,
        string nowText,
        CancellationToken cancellationToken)
    {
        var recentQuery = _dbContext.Settlements.AsNoTracking()
            .Where(s => s.CreatedAt.HasValue && s.CreatedAt.Value >= threshold);

        var total = await recentQuery.CountAsync(cancellationToken);
        var draftCount = await recentQuery.CountAsync(s => s.Status == "draft", cancellationToken);
        var pendingCount = await recentQuery.CountAsync(s => s.Status == "pending_approval", cancellationToken);
        var approvedCount = await recentQuery.CountAsync(s => s.Status == "approved", cancellationToken);
        var paidCount = await recentQuery.CountAsync(s => s.Status == "paid", cancellationToken);
        var archivedCount = await recentQuery.CountAsync(s => s.Status == "archived", cancellationToken);
        var prePaymentCount = await recentQuery.CountAsync(s => s.Type == "pre_payment", cancellationToken);
        var monthlyCount = await recentQuery.CountAsync(s => s.Type == "monthly", cancellationToken);
        var quarterlyCount = await recentQuery.CountAsync(s => s.Type == "quarterly", cancellationToken);

        var overdueCount = await _dbContext.Settlements.AsNoTracking()
            .Where(s => s.Status == "approved"
                        && s.PaymentDueDate != null
                        && s.CreatedAt.HasValue
                        && s.CreatedAt.Value >= threshold
                        && string.Compare(s.PaymentDueDate, nowText) < 0)
            .CountAsync(cancellationToken);

        return new SettlementProgressTracking
        {
            Total = total,
            DraftCount = draftCount,
            PendingApprovalCount = pendingCount,
            ApprovedCount = approvedCount,
            PaidCount = paidCount,
            ArchivedCount = archivedCount,
            PrePaymentCount = prePaymentCount,
            MonthlyCount = monthlyCount,
            QuarterlyCount = quarterlyCount,
            OverdueCount = overdueCount,
        };
    }

    public void AddSettlement(Settlement settlement)
    {
        _dbContext.Settlements.Add(settlement);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SettlementListItem
{
    public required Settlement Settlement { get; init; }
    public string? SupplierName { get; init; }
    public string? SupplierStage { get; init; }
    public string? CreatorName { get; init; }
    public string? RfqTitle { get; init; }
}

public sealed class SettlementStatementSupplier
{
    public int Id { get; init; }
    public string? CompanyName { get; init; }
    public string? BankAccount { get; init; }
    public string? ContactEmail { get; init; }
}

public sealed class SettlementStatementInvoice
{
    public int Id { get; init; }
    public string? InvoiceNumber { get; init; }
    public decimal Amount { get; init; }
    public string? TaxRate { get; init; }
    public DateTime InvoiceDate { get; init; }
    public int? RfqId { get; init; }
    public string? RfqTitle { get; init; }
}

public sealed class SettlementProgressTracking
{
    public int Total { get; init; }
    public int DraftCount { get; init; }
    public int PendingApprovalCount { get; init; }
    public int ApprovedCount { get; init; }
    public int PaidCount { get; init; }
    public int ArchivedCount { get; init; }
    public int PrePaymentCount { get; init; }
    public int MonthlyCount { get; init; }
    public int QuarterlyCount { get; init; }
    public int OverdueCount { get; init; }
}
