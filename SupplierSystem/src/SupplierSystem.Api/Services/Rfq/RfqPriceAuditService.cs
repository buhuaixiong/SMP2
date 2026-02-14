using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqPriceAuditService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly ILogger<RfqPriceAuditService> _logger;

    public RfqPriceAuditService(SupplierSystemDbContext dbContext, ILogger<RfqPriceAuditService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task UpsertQuoteAuditAsync(Quote quote, string? ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var rfq = await _dbContext.Rfqs.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == quote.RfqId, cancellationToken);
            if (rfq == null)
            {
                return;
            }

            var supplier = await _dbContext.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == quote.SupplierId, cancellationToken);

            var quoteLineItems = await _dbContext.QuoteLineItems.AsNoTracking()
                .Where(qli => qli.QuoteId == quote.Id)
                .ToListAsync(cancellationToken);

            if (quoteLineItems.Count == 0)
            {
                await UpsertRfqLevelRecordAsync(rfq, quote, supplier, ipAddress, cancellationToken);
                return;
            }

            var lineItems = await _dbContext.RfqLineItems.AsNoTracking()
                .Where(li => li.RfqId == quote.RfqId)
                .ToListAsync(cancellationToken);
            var lineItemMap = lineItems.ToDictionary(li => li.Id);
            var lineItemIds = quoteLineItems.Select(qli => qli.RfqLineItemId).Distinct().ToList();

            var existing = await _dbContext.RfqPriceAuditRecords
                .Where(r => r.RfqId == quote.RfqId &&
                            r.SupplierId == quote.SupplierId &&
                            r.RfqLineItemId != null &&
                            lineItemIds.Contains(r.RfqLineItemId.Value))
                .ToListAsync(cancellationToken);
            var existingMap = existing.ToDictionary(r => r.RfqLineItemId!.Value);

            var now = DateTime.UtcNow.ToString("o");
            foreach (var qli in quoteLineItems)
            {
                if (!lineItemMap.TryGetValue(qli.RfqLineItemId, out var lineItem))
                {
                    continue;
                }

                var record = existingMap.TryGetValue(qli.RfqLineItemId, out var existingRecord)
                    ? existingRecord
                    : new RfqPriceAuditRecord
                    {
                        RfqId = quote.RfqId,
                        RfqLineItemId = qli.RfqLineItemId,
                        CreatedAt = now,
                    };

                record.RfqTitle = rfq.Title;
                record.RfqCreatedAt = rfq.CreatedAt;
                record.LineNumber = lineItem.LineNumber;
                record.Quantity = lineItem.Quantity;
                record.QuoteId = quote.Id;
                record.SupplierId = quote.SupplierId;
                record.SupplierName = supplier?.CompanyName;
                record.SupplierIp = NormalizeIp(ipAddress ?? quote.IpAddress);
                record.QuotedUnitPrice = qli.UnitPrice;
                record.QuotedTotalPrice = qli.TotalPrice;
                record.QuoteCurrency = quote.Currency;
                record.QuoteSubmittedAt = quote.SubmittedAt;
                record.ApprovalStatus = rfq.ApprovalStatus;
                record.SelectedQuoteId = lineItem.SelectedQuoteId;
                if (lineItem.SelectedQuoteId == quote.Id)
                {
                    record.SelectedSupplierId = quote.SupplierId;
                    record.SelectedSupplierName = supplier?.CompanyName;
                    record.SelectedUnitPrice = qli.UnitPrice;
                    record.SelectedCurrency = quote.Currency;
                }

                record.UpdatedAt = now;

                if (existingRecord == null)
                {
                    _dbContext.RfqPriceAuditRecords.Add(record);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upsert RFQ price audit record for quote {QuoteId}", quote.Id);
        }
    }

    public async Task SyncSelectedQuoteForLineItemAsync(
        long lineItemId,
        long? selectedQuoteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await _dbContext.RfqPriceAuditRecords
                .Where(r => r.RfqLineItemId == lineItemId)
                .ToListAsync(cancellationToken);
            if (records.Count == 0)
            {
                return;
            }

            Quote? quote = null;
            QuoteLineItem? quoteLineItem = null;
            Supplier? supplier = null;
            if (selectedQuoteId.HasValue)
            {
                quote = await _dbContext.Quotes.AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == selectedQuoteId.Value, cancellationToken);
                if (quote != null)
                {
                    supplier = await _dbContext.Suppliers.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == quote.SupplierId, cancellationToken);
                    quoteLineItem = await _dbContext.QuoteLineItems.AsNoTracking()
                        .FirstOrDefaultAsync(qli => qli.QuoteId == quote.Id && qli.RfqLineItemId == lineItemId, cancellationToken);
                }
            }

            var now = DateTime.UtcNow.ToString("o");
            foreach (var record in records)
            {
                record.SelectedQuoteId = selectedQuoteId;
                if (selectedQuoteId.HasValue && record.QuoteId == selectedQuoteId)
                {
                    record.SelectedSupplierId = quote?.SupplierId;
                    record.SelectedSupplierName = supplier?.CompanyName;
                    record.SelectedUnitPrice = quoteLineItem?.UnitPrice;
                    record.SelectedCurrency = quote?.Currency;
                }
                else
                {
                    record.SelectedSupplierId = null;
                    record.SelectedSupplierName = null;
                    record.SelectedUnitPrice = null;
                    record.SelectedCurrency = null;
                }

                record.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync selected quote for line item {LineItemId}", lineItemId);
        }
    }

    public async Task SyncSelectedQuoteForRfqAsync(
        long rfqId,
        long? selectedQuoteId,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await _dbContext.RfqPriceAuditRecords
                .Where(r => r.RfqId == rfqId)
                .ToListAsync(cancellationToken);
            if (records.Count == 0)
            {
                return;
            }

            Quote? quote = null;
            Supplier? supplier = null;
            Dictionary<long, QuoteLineItem>? quoteLineItemMap = null;
            if (selectedQuoteId.HasValue)
            {
                quote = await _dbContext.Quotes.AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == selectedQuoteId.Value, cancellationToken);
                if (quote != null)
                {
                    supplier = await _dbContext.Suppliers.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == quote.SupplierId, cancellationToken);
                    var quoteLineItems = await _dbContext.QuoteLineItems.AsNoTracking()
                        .Where(qli => qli.QuoteId == quote.Id)
                        .ToListAsync(cancellationToken);
                    quoteLineItemMap = quoteLineItems.ToDictionary(qli => qli.RfqLineItemId);
                }
            }

            var now = DateTime.UtcNow.ToString("o");
            foreach (var record in records)
            {
                record.SelectedQuoteId = selectedQuoteId;
                if (selectedQuoteId.HasValue && quote != null)
                {
                    record.SelectedSupplierId = quote.SupplierId;
                    record.SelectedSupplierName = supplier?.CompanyName;
                    record.SelectedCurrency = quote.Currency;
                    if (record.RfqLineItemId.HasValue &&
                        quoteLineItemMap != null &&
                        quoteLineItemMap.TryGetValue(record.RfqLineItemId.Value, out var selectedItem))
                    {
                        record.SelectedUnitPrice = selectedItem.UnitPrice;
                    }
                    else
                    {
                        record.SelectedUnitPrice = null;
                    }
                }
                else
                {
                    record.SelectedSupplierId = null;
                    record.SelectedSupplierName = null;
                    record.SelectedUnitPrice = null;
                    record.SelectedCurrency = null;
                }

                record.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync selected quote for RFQ {RfqId}", rfqId);
        }
    }

    public async Task UpdateApprovalForRfqAsync(
        long rfqId,
        string? approvalStatus,
        string? decision,
        string? decidedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await _dbContext.RfqPriceAuditRecords
                .Where(r => r.RfqId == rfqId)
                .ToListAsync(cancellationToken);
            if (records.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow.ToString("o");
            foreach (var record in records)
            {
                record.ApprovalStatus = approvalStatus;
                record.ApprovalDecision = decision;
                record.ApprovalDecidedAt = decidedAt;
                record.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update approval status for RFQ {RfqId}", rfqId);
        }
    }

    public async Task UpdateApprovalForLineItemAsync(
        long lineItemId,
        string? approvalStatus,
        string? decision,
        string? decidedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var records = await _dbContext.RfqPriceAuditRecords
                .Where(r => r.RfqLineItemId == lineItemId)
                .ToListAsync(cancellationToken);
            if (records.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow.ToString("o");
            foreach (var record in records)
            {
                record.ApprovalStatus = approvalStatus;
                record.ApprovalDecision = decision;
                record.ApprovalDecidedAt = decidedAt;
                record.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update approval status for line item {LineItemId}", lineItemId);
        }
    }

    public async Task UpdatePrExportAsync(
        long rfqId,
        IReadOnlyCollection<long> lineItemIds,
        string? filledBy,
        string? filledAt,
        CancellationToken cancellationToken)
    {
        try
        {
            if (lineItemIds.Count == 0)
            {
                return;
            }

            var records = await _dbContext.RfqPriceAuditRecords
                .Where(r => r.RfqId == rfqId && r.RfqLineItemId != null && lineItemIds.Contains(r.RfqLineItemId.Value))
                .ToListAsync(cancellationToken);
            if (records.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow.ToString("o");
            foreach (var record in records)
            {
                record.PrFilledBy = filledBy;
                record.PrFilledAt = filledAt;
                record.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update PR export info for RFQ {RfqId}", rfqId);
        }
    }

    private async Task UpsertRfqLevelRecordAsync(
        SupplierSystem.Domain.Entities.Rfq rfq,
        Quote quote,
        Supplier? supplier,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.ToString("o");
        var record = await _dbContext.RfqPriceAuditRecords
            .FirstOrDefaultAsync(r => r.RfqId == rfq.Id && r.RfqLineItemId == null && r.SupplierId == quote.SupplierId,
                cancellationToken);

        if (record == null)
        {
            record = new RfqPriceAuditRecord
            {
                RfqId = rfq.Id,
                SupplierId = quote.SupplierId,
                CreatedAt = now,
            };

            _dbContext.RfqPriceAuditRecords.Add(record);
        }

        record.RfqTitle = rfq.Title;
        record.RfqCreatedAt = rfq.CreatedAt;
        record.QuoteId = quote.Id;
        record.SupplierName = supplier?.CompanyName;
        record.SupplierIp = NormalizeIp(ipAddress ?? quote.IpAddress);
        record.QuotedUnitPrice = null;
        record.QuotedTotalPrice = quote.TotalAmount;
        record.QuoteCurrency = quote.Currency;
        record.QuoteSubmittedAt = quote.SubmittedAt;
        record.ApprovalStatus = rfq.ApprovalStatus;
        record.SelectedQuoteId = rfq.SelectedQuoteId;

        if (rfq.SelectedQuoteId == quote.Id)
        {
            record.SelectedSupplierId = quote.SupplierId;
            record.SelectedSupplierName = supplier?.CompanyName;
            record.SelectedUnitPrice = null;
            record.SelectedCurrency = quote.Currency;
        }
        else
        {
            record.SelectedSupplierId = null;
            record.SelectedSupplierName = null;
            record.SelectedUnitPrice = null;
            record.SelectedCurrency = null;
        }

        record.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeIp(string? ipAddress)
    {
        return string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();
    }
}
