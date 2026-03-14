using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services;

public sealed class RfqControllerDataService
{
    private readonly SupplierSystemDbContext _dbContext;

    public RfqControllerDataService(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<RfqLineItemExportRecord>> LoadLineItemExportRecordsAsync(
        long rfqId,
        IReadOnlyList<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        return await (from li in _dbContext.RfqLineItems.AsNoTracking()
                      join q in _dbContext.Quotes.AsNoTracking()
                          on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                      from q in quoteGroup.DefaultIfEmpty()
                      where li.RfqId == rfqId && lineItemIds.Contains(li.Id)
                      select new RfqLineItemExportRecord
                      {
                          Id = li.Id,
                          LineNumber = li.LineNumber,
                          Status = li.Status,
                          SelectedQuoteId = li.SelectedQuoteId,
                          QuoteStatus = q != null ? q.Status : null,
                      })
            .ToListAsync(cancellationToken);
    }

    public Task<Quote?> FindQuoteAsync(long quoteId, CancellationToken cancellationToken)
    {
        return _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);
    }

    public Task<RfqPrRecord?> FindRfqPrRecordAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqPrRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(pr => pr.RfqId == rfqId, cancellationToken);
    }

    public Task<SupplierSystem.Domain.Entities.Rfq?> FindRfqAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
    }

    public Task<List<RfqLineItem>> LoadLineItemsAsync(long rfqId, IReadOnlyList<long> lineItemIds, CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems
            .Where(li => li.RfqId == rfqId && lineItemIds.Contains(li.Id))
            .ToListAsync(cancellationToken);
    }

    public void AddPrRecord(RfqPrRecord record)
    {
        _dbContext.RfqPrRecords.Add(record);
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

public sealed class RfqLineItemExportRecord
{
    public long Id { get; set; }
    public int? LineNumber { get; set; }
    public string? Status { get; set; }
    public long? SelectedQuoteId { get; set; }
    public string? QuoteStatus { get; set; }
    public string? Reason { get; set; }
}
