using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using DomainRfq = SupplierSystem.Domain.Entities.Rfq;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqQuoteService(
    SupplierSystemDbContext dbContext,
    QuoteStateMachine quoteStateMachine,
    RfqPriceAuditService priceAuditService) : NodeServiceBase
{
    private readonly SupplierSystemDbContext _dbContext = dbContext;
    private readonly QuoteStateMachine _quoteStateMachine = quoteStateMachine;
    private readonly RfqPriceAuditService _priceAuditService = priceAuditService;

    public Task<Dictionary<string, object?>> SubmitQuoteAsync(
        int rfqId,
        Models.Rfq.SubmitQuoteRequest request,
        AuthUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return SubmitQuoteInternalAsync(rfqId, request, user, ipAddress, cancellationToken);
    }

    public Task<Dictionary<string, object?>> UpdateQuoteAsync(int rfqId, int quoteId, Models.Rfq.SubmitQuoteRequest request, AuthUser user, CancellationToken cancellationToken)
    {
        return UpdateQuoteInternalAsync(rfqId, quoteId, request, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> WithdrawQuoteAsync(int rfqId, int quoteId, AuthUser user, CancellationToken cancellationToken)
    {
        return WithdrawQuoteInternalAsync(rfqId, quoteId, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetQuoteDetailsAsync(int quoteId, AuthUser user, CancellationToken cancellationToken)
    {
        return GetQuoteDetailsInternalAsync(quoteId, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> ComparePricesAsync(int rfqId, AuthUser user, CancellationToken cancellationToken)
    {
        return ComparePricesInternalAsync(rfqId, user, cancellationToken);
    }

    private async Task<Dictionary<string, object?>> SubmitQuoteInternalAsync(
        int rfqId,
        Models.Rfq.SubmitQuoteRequest request,
        AuthUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (!user.SupplierId.HasValue)
        {
            throw new ServiceErrorException(403, "Only suppliers can submit quotes");
        }

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {rfqId} not found");
        }

        ValidateQuoteSubmission(rfq, user);

        ValidateRequired(new Dictionary<string, object?>
        {
            ["totalPrice"] = request.TotalPrice,
            ["currency"] = request.Currency,
            ["deliveryPeriod"] = request.DeliveryPeriod,
        }, new[] { "totalPrice", "currency", "deliveryPeriod" });

        var now = DateTime.UtcNow.ToString("o");

        var existingQuotes = await _dbContext.Quotes
            .Where(q => q.RfqId == rfqId && q.SupplierId == user.SupplierId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var existing in existingQuotes)
        {
            existing.IsLatest = false;
        }

        var quote = new Quote
        {
            RfqId = rfqId,
            SupplierId = user.SupplierId.Value,
            TotalAmount = request.TotalPrice,
            Currency = request.Currency,
            DeliveryDate = request.DeliveryPeriod,
            DeliveryTerms = request.DeliveryTerms,
            Notes = request.Remarks,
            Status = "draft",
            IsLatest = true,
            CreatedAt = now,
            UpdatedAt = now,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
        };

        _dbContext.Quotes.Add(quote);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (request.Items != null && request.Items.Count > 0)
        {
            var lineItems = await _dbContext.RfqLineItems.AsNoTracking()
                .Where(li => li.RfqId == rfqId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var lineItemLookup = lineItems.ToDictionary(li => li.LineNumber, li => li.Id);

            var index = 1;
            foreach (var item in request.Items)
            {
                var lineNumber = item.LineNumber ?? index;
                if (!lineItemLookup.TryGetValue(lineNumber, out var rfqLineItemId))
                {
                    index++;
                    continue;
                }

                var qli = new QuoteLineItem
                {
                    QuoteId = quote.Id,
                    RfqLineItemId = rfqLineItemId,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice ?? ((item.UnitPrice ?? 0) * (item.Quantity ?? 0)),
                    Notes = item.Remarks,
                };

                _dbContext.QuoteLineItems.Add(qli);
                index++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var updated = await _quoteStateMachine.TransitionAsync(
                quote,
                QuoteStateMachine.Statuses.Submitted,
                user,
                null,
                async (entity, status, token) =>
                {
                    entity.Status = status;
                    entity.SubmittedAt = now;
                    entity.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync(token).ConfigureAwait(false);
                    return entity;
                },
                cancellationToken)
            .ConfigureAwait(false);

        await _priceAuditService.UpsertQuoteAuditAsync(updated, ipAddress, cancellationToken);

        return MapQuoteToLegacyDictionary(updated);
    }

    private async Task<Dictionary<string, object?>> UpdateQuoteInternalAsync(
        int rfqId,
        int quoteId,
        Models.Rfq.SubmitQuoteRequest request,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var quote = await _dbContext.Quotes
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == rfqId, cancellationToken)
            .ConfigureAwait(false);

        if (quote == null)
        {
            throw new Exception($"Quote with id {quoteId} not found");
        }

        if (quote.SupplierId != user.SupplierId)
        {
            throw new ServiceErrorException(403, "Cannot update other supplier's quote");
        }

        if (quote.Status != "draft" && quote.Status != "withdrawn")
        {
            throw new ServiceErrorException(400, "Cannot update submitted quote");
        }

        if (request.TotalPrice.HasValue)
        {
            quote.TotalAmount = request.TotalPrice;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            quote.Currency = request.Currency;
        }

        if (!string.IsNullOrWhiteSpace(request.DeliveryPeriod))
        {
            quote.DeliveryDate = request.DeliveryPeriod;
        }

        if (request.Remarks != null)
        {
            quote.Notes = request.Remarks;
        }

        quote.UpdatedAt = DateTime.UtcNow.ToString("o");

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapQuoteToLegacyDictionary(quote);
    }

    private async Task<Dictionary<string, object?>> WithdrawQuoteInternalAsync(
        int rfqId,
        int quoteId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var quote = await _dbContext.Quotes
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == rfqId, cancellationToken)
            .ConfigureAwait(false);

        if (quote == null)
        {
            throw new Exception($"Quote with id {quoteId} not found");
        }

        if (quote.SupplierId != user.SupplierId)
        {
            throw new ServiceErrorException(403, "Cannot withdraw other supplier's quote");
        }

        if (quote.Status != "submitted")
        {
            throw new ServiceErrorException(400, "Can only withdraw submitted quote");
        }

        var now = DateTime.UtcNow.ToString("o");
        var updated = await _quoteStateMachine.TransitionAsync(
                quote,
                QuoteStateMachine.Statuses.Withdrawn,
                user,
                "Supplier withdrew quote",
                async (entity, status, token) =>
                {
                    entity.Status = status;
                    entity.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync(token).ConfigureAwait(false);
                    return entity;
                },
                cancellationToken)
            .ConfigureAwait(false);

        return MapQuoteToLegacyDictionary(updated);
    }

    private async Task<Dictionary<string, object?>> GetQuoteDetailsInternalAsync(
        int quoteId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var quote = await _dbContext.Quotes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken)
            .ConfigureAwait(false);

        if (quote == null)
        {
            throw new Exception($"Quote with id {quoteId} not found");
        }

        var canView = quote.SupplierId == user.SupplierId ||
                      HasAnyPermission(user, new[] { Permissions.RfqViewQuotes });

        if (!canView)
        {
            throw new ServiceErrorException(403, "No permission to view this quote");
        }

        var rfqTitle = await _dbContext.Rfqs.AsNoTracking()
            .Where(r => r.Id == quote.RfqId)
            .Select(r => r.Title)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var supplierName = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.Id == quote.SupplierId)
            .Select(s => s.CompanyName)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await (from qli in _dbContext.QuoteLineItems.AsNoTracking()
                           join li in _dbContext.RfqLineItems.AsNoTracking()
                               on qli.RfqLineItemId equals li.Id
                           where qli.QuoteId == quote.Id
                           orderby li.LineNumber
                           select new
                           {
                               qli,
                               li.LineNumber,
                               li.ItemName,
                               li.Quantity
                           })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var itemList = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            itemList.Add(new Dictionary<string, object?>
            {
                ["id"] = item.qli.Id,
                ["quote_id"] = quote.Id,
                ["line_number"] = item.LineNumber,
                ["description"] = item.ItemName,
                ["quantity"] = item.Quantity,
                ["unit_price"] = item.qli.UnitPrice,
                ["total_price"] = item.qli.TotalPrice,
                ["remarks"] = item.qli.Notes,
            });
        }

        var result = MapQuoteToLegacyDictionary(quote);
        result["rfq_title"] = rfqTitle;
        result["supplier_name"] = supplierName;
        result["items"] = itemList;

        return result;
    }

    private async Task<Dictionary<string, object?>> ComparePricesInternalAsync(
        int rfqId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqViewQuotes });

        var quotes = await (from q in _dbContext.Quotes.AsNoTracking()
                            join s in _dbContext.Suppliers.AsNoTracking() on q.SupplierId equals s.Id into supplierGroup
                            from s in supplierGroup.DefaultIfEmpty()
                            where q.RfqId == rfqId && q.Status == "submitted" && q.IsLatest
                            orderby q.TotalAmount
                            select new
                            {
                                q.Id,
                                q.TotalAmount,
                                q.Currency,
                                q.DeliveryDate,
                                q.SubmittedAt,
                                SupplierName = s != null ? s.CompanyName : null,
                            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var quoteList = quotes.Select(q => new Dictionary<string, object?>
        {
            ["id"] = q.Id,
            ["total_price"] = q.TotalAmount,
            ["currency"] = q.Currency,
            ["delivery_period"] = q.DeliveryDate,
            ["submitted_at"] = q.SubmittedAt,
            ["supplier_name"] = q.SupplierName,
        }).ToList();

        var analysis = new Dictionary<string, object?>
        {
            ["quoteCount"] = quoteList.Count,
            ["lowestPrice"] = quoteList.Count > 0 ? quoteList[0]["total_price"] : null,
            ["highestPrice"] = quoteList.Count > 0 ? quoteList[^1]["total_price"] : null,
            ["averagePrice"] = quoteList.Count > 0
                ? quoteList.Average(q => q["total_price"] is decimal price ? price : 0)
                : null,
            ["quotes"] = quoteList,
        };

        return analysis;
    }

    private void ValidateQuoteSubmission(DomainRfq rfq, AuthUser user)
    {
        if (rfq.Status != "published" && rfq.Status != "in_progress")
        {
            throw new ServiceErrorException(400, "RFQ is not accepting quotes");
        }

        if (!string.IsNullOrWhiteSpace(rfq.ValidUntil) &&
            DateTime.TryParse(rfq.ValidUntil, out var deadline) &&
            deadline < DateTime.UtcNow)
        {
            throw new ServiceErrorException(400, "RFQ deadline has passed");
        }

        var invitation = _dbContext.SupplierRfqInvitations.AsNoTracking()
            .FirstOrDefault(i => i.RfqId == rfq.Id && i.SupplierId == user.SupplierId);

        if (invitation == null)
        {
            throw new ServiceErrorException(403, "Supplier not invited to this RFQ");
        }

        if (invitation.Status != null &&
            (invitation.Status.Equals("declined", StringComparison.OrdinalIgnoreCase) ||
             invitation.Status.Equals("revoked", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ServiceErrorException(403, "Invitation is no longer active");
        }
    }

    private static Dictionary<string, object?> MapQuoteToLegacyDictionary(Quote quote)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = quote.Id,
            ["rfq_id"] = quote.RfqId,
            ["supplier_id"] = quote.SupplierId,
            ["total_price"] = quote.TotalAmount,
            ["currency"] = quote.Currency,
            ["delivery_period"] = quote.DeliveryDate,
            ["delivery_terms"] = quote.DeliveryTerms,
            ["remarks"] = quote.Notes,
            ["status"] = quote.Status,
            ["submitted_at"] = quote.SubmittedAt,
            ["is_latest"] = quote.IsLatest ? 1 : 0,
            ["created_at"] = quote.CreatedAt,
            ["updated_at"] = quote.UpdatedAt,
            ["withdrawal_reason"] = quote.WithdrawalReason,
            ["withdrawn_at"] = quote.WithdrawnAt,
        };
    }
}

