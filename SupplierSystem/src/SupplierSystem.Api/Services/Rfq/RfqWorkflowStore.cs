using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqWorkflowStore
{
    private static readonly string[] ClosedBidRoundStatuses = ["closed", "cancelled"];
    private readonly SupplierSystemDbContext _dbContext;

    public RfqWorkflowStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<SupplierSystem.Domain.Entities.Rfq> QueryRfqs(bool asNoTracking = true)
    {
        var query = _dbContext.Rfqs.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<List<long>> LoadInvitedRfqIdsAsync(int supplierId, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierRfqInvitations.AsNoTracking()
            .Where(inv => inv.SupplierId == supplierId)
            .Select(inv => (long)inv.RfqId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public IQueryable<RfqApproval> QueryApprovals(bool asNoTracking = true)
    {
        var query = _dbContext.RfqApprovals.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public IQueryable<ApprovalComment> QueryApprovalComments(bool asNoTracking = true)
    {
        var query = _dbContext.ApprovalComments.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<RfqApproval?> FindApprovalAsync(int approvalId, int rfqId, bool asNoTracking, CancellationToken cancellationToken)
    {
        return QueryApprovals(asNoTracking)
            .FirstOrDefaultAsync(a => a.Id == approvalId && a.RfqId == rfqId, cancellationToken);
    }

    public Task<RfqApproval?> FindNextPendingApprovalAsync(int rfqId, int currentStepOrder, CancellationToken cancellationToken)
    {
        return QueryApprovals()
            .Where(a => a.RfqId == rfqId && a.StepOrder > currentStepOrder && a.Status == "pending")
            .OrderBy(a => a.StepOrder)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<SupplierSystem.Domain.Entities.Rfq?> FindRfqAsync(long id, bool asNoTracking, CancellationToken cancellationToken)
    {
        return QueryRfqs(asNoTracking)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public IQueryable<RfqBidRound> QueryBidRounds(bool asNoTracking = true)
    {
        var query = _dbContext.RfqBidRounds.AsQueryable();
        return asNoTracking ? query.AsNoTracking() : query;
    }

    public Task<RfqBidRound?> FindLatestBidRoundAsync(long rfqId, bool asNoTracking, CancellationToken cancellationToken)
    {
        return QueryBidRounds(asNoTracking)
            .Where(round => round.RfqId == rfqId)
            .OrderByDescending(round => round.RoundNumber)
            .ThenByDescending(round => round.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RfqBidRound?> FindCurrentBidRoundAsync(long rfqId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var currentRound = await QueryBidRounds(asNoTracking)
            .Where(round => round.RfqId == rfqId &&
                            (round.Status == null ||
                             !ClosedBidRoundStatuses.Contains(round.Status.ToLower())))
            .OrderByDescending(round => round.RoundNumber)
            .ThenByDescending(round => round.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return currentRound ?? await FindLatestBidRoundAsync(rfqId, asNoTracking, cancellationToken);
    }

    public Task<List<RfqBidRound>> LoadBidRoundsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqBidRounds.AsNoTracking()
            .Where(round => round.RfqId == rfqId)
            .OrderByDescending(round => round.RoundNumber)
            .ThenByDescending(round => round.Id)
            .ToListAsync(cancellationToken);
    }

    public void AddBidRound(RfqBidRound bidRound)
    {
        _dbContext.RfqBidRounds.Add(bidRound);
    }

    public Task<Quote?> FindQuoteAsync(long quoteId, long rfqId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.Quotes.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == rfqId, cancellationToken);
    }

    public Task<Quote?> FindQuoteByIdAsync(long quoteId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.Quotes.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);
    }

    public Task<bool> HasAnyRfqLineItemsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems.AsNoTracking().AnyAsync(li => li.RfqId == rfqId, cancellationToken);
    }

    public Task<bool> HasAnyRfqApprovalsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqApprovals.AsNoTracking().AnyAsync(a => a.RfqId == rfqId, cancellationToken);
    }

    public Task<SupplierRfqInvitation?> FindInvitationAsync(long rfqId, int supplierId, CancellationToken cancellationToken)
    {
        return FindInvitationAsync(rfqId, supplierId, bidRoundId: null, cancellationToken);
    }

    public Task<SupplierRfqInvitation?> FindInvitationAsync(long rfqId, int supplierId, long? bidRoundId, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierRfqInvitations.AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.RfqId == rfqId &&
                     i.SupplierId == supplierId &&
                     (bidRoundId == null ? i.BidRoundId == null : i.BidRoundId == bidRoundId),
                cancellationToken);
    }

    public Task<bool> HasQuoteAsync(long rfqId, int supplierId, CancellationToken cancellationToken)
    {
        return HasQuoteAsync(rfqId, supplierId, bidRoundId: null, cancellationToken);
    }

    public Task<bool> HasQuoteAsync(long rfqId, int supplierId, long? bidRoundId, CancellationToken cancellationToken)
    {
        return _dbContext.Quotes.AsNoTracking()
            .AnyAsync(
                q => q.RfqId == rfqId &&
                     q.SupplierId == supplierId &&
                     (bidRoundId == null ? q.BidRoundId == null : q.BidRoundId == bidRoundId),
                cancellationToken);
    }

    public Task<List<RfqLineItemQuantityRecord>> LoadRfqLineItemQuantitiesAsync(
        long rfqId,
        IReadOnlyCollection<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems.AsNoTracking()
            .Where(li => li.RfqId == rfqId && lineItemIds.Contains(li.Id))
            .Select(li => new RfqLineItemQuantityRecord
            {
                Id = li.Id,
                Quantity = li.Quantity,
            })
            .ToListAsync(cancellationToken);
    }

    public void AddQuote(Quote quote)
    {
        _dbContext.Quotes.Add(quote);
    }

    public void AddQuoteLineItem(QuoteLineItem item)
    {
        _dbContext.QuoteLineItems.Add(item);
    }

    public void AddQuoteAttachment(QuoteAttachment attachment)
    {
        _dbContext.QuoteAttachments.Add(attachment);
    }

    public Task<List<QuoteLineItem>> LoadQuoteLineItemsAsync(long quoteId, CancellationToken cancellationToken)
    {
        return _dbContext.QuoteLineItems
            .Where(li => li.QuoteId == quoteId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveQuoteLineItems(IEnumerable<QuoteLineItem> items)
    {
        _dbContext.QuoteLineItems.RemoveRange(items);
    }

    public Task<Quote?> FindQuoteForSupplierAsync(long quoteId, long rfqId, int supplierId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.Quotes.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == rfqId && q.SupplierId == supplierId, cancellationToken);
    }

    public IQueryable<QuoteListRow> QueryQuoteListRows(long rfqId, long? bidRoundId = null)
    {
        return from q in _dbContext.Quotes.AsNoTracking()
               join s in _dbContext.Suppliers.AsNoTracking() on q.SupplierId equals s.Id
               where q.RfqId == rfqId &&
                     (bidRoundId == null ? q.BidRoundId == null : q.BidRoundId == bidRoundId)
               select new QuoteListRow
               {
                   Quote = q,
                   CompanyName = s.CompanyName,
                   CompanyId = s.CompanyId,
                   Stage = s.Stage,
                };
    }

    public Task<List<RfqInvitedSupplierRow>> LoadInvitedSupplierRowsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return LoadInvitedSupplierRowsAsync(rfqId, bidRoundId: null, cancellationToken);
    }

    public Task<List<RfqInvitedSupplierRow>> LoadInvitedSupplierRowsAsync(long rfqId, long? bidRoundId, CancellationToken cancellationToken)
    {
        return (from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
                join s in _dbContext.Suppliers.AsNoTracking() on inv.SupplierId equals s.Id
                join q in _dbContext.Quotes.AsNoTracking()
                    .Where(q => q.RfqId == rfqId &&
                                q.IsLatest &&
                                (bidRoundId == null ? q.BidRoundId == null : q.BidRoundId == bidRoundId))
                    on s.Id equals q.SupplierId into quoteGroup
                from latestQuote in quoteGroup.DefaultIfEmpty()
                where inv.RfqId == rfqId &&
                      (bidRoundId == null ? inv.BidRoundId == null : inv.BidRoundId == bidRoundId)
                select new RfqInvitedSupplierRow
                {
                    Id = s.Id,
                    CompanyId = s.CompanyId,
                    SupplierCode = s.SupplierCode,
                    VendorCode = s.SupplierCode ?? s.CompanyId,
                    CompanyName = s.CompanyName,
                    Stage = s.Stage,
                    InvitationStatus = inv.Status,
                    QuoteStatus = latestQuote != null ? latestQuote.Status : null,
                    QuoteSubmittedAt = latestQuote != null ? latestQuote.SubmittedAt : null,
                })
            .ToListAsync(cancellationToken);
    }

    public Task<List<PriceComparisonAttachment>> LoadPriceComparisonAttachmentsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return LoadPriceComparisonAttachmentsAsync(rfqId, bidRoundId: null, cancellationToken);
    }

    public Task<List<PriceComparisonAttachment>> LoadPriceComparisonAttachmentsAsync(long rfqId, long? bidRoundId, CancellationToken cancellationToken)
    {
        return _dbContext.PriceComparisonAttachments.AsNoTracking()
            .Where(att => att.RfqId == rfqId &&
                          (bidRoundId == null ? att.BidRoundId == null : att.BidRoundId == bidRoundId))
            .OrderByDescending(att => att.UploadedAt)
            .ThenByDescending(att => att.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<PriceComparisonAttachment>> LoadAllRoundPriceComparisonAttachmentsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.PriceComparisonAttachments.AsNoTracking()
            .Where(att => att.RfqId == rfqId)
            .OrderByDescending(att => att.BidRoundId)
            .ThenByDescending(att => att.UploadedAt)
            .ThenByDescending(att => att.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<RfqApproval>> LoadApprovalsForRfqAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqApprovals.AsNoTracking()
            .Where(a => a.RfqId == rfqId)
            .OrderBy(a => a.StepOrder)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Quote>> LoadQuotesForSupplierAsync(long rfqId, int supplierId, CancellationToken cancellationToken)
    {
        return LoadQuotesForSupplierAsync(rfqId, supplierId, bidRoundId: null, cancellationToken);
    }

    public Task<List<Quote>> LoadQuotesForSupplierAsync(long rfqId, int supplierId, long? bidRoundId, CancellationToken cancellationToken)
    {
        return _dbContext.Quotes.AsNoTracking()
            .Where(q => q.RfqId == rfqId &&
                        q.SupplierId == supplierId &&
                        (bidRoundId == null ? q.BidRoundId == null : q.BidRoundId == bidRoundId))
            .OrderByDescending(q => q.SubmittedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<QuoteLineItemWithQuantityRow>> LoadQuoteLineItemRowsAsync(long quoteId, CancellationToken cancellationToken)
    {
        return (from qli in _dbContext.QuoteLineItems.AsNoTracking()
                join rli in _dbContext.RfqLineItems.AsNoTracking()
                    on qli.RfqLineItemId equals rli.Id into rfqGroup
                from rli in rfqGroup.DefaultIfEmpty()
                where qli.QuoteId == quoteId
                orderby qli.Id
                select new QuoteLineItemWithQuantityRow
                {
                    QuoteLineItem = qli,
                    RfqQuantity = rli != null ? rli.Quantity : null,
                })
            .ToListAsync(cancellationToken);
    }

    public Task<long?> FindRfqIdForQuoteAsync(long quoteId, CancellationToken cancellationToken)
    {
        return _dbContext.Quotes.AsNoTracking()
            .Where(q => q.Id == quoteId)
            .Select(q => (long?)q.RfqId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<QuoteAttachment>> LoadQuoteAttachmentsAsync(long quoteId, CancellationToken cancellationToken)
    {
        return _dbContext.QuoteAttachments.AsNoTracking()
            .Where(att => att.QuoteId == quoteId)
            .OrderByDescending(att => att.UploadedAt)
            .ThenByDescending(att => att.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<RfqLineItem>> LoadOrderedRfqLineItemsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems.AsNoTracking()
            .Where(li => li.RfqId == rfqId)
            .OrderBy(li => li.LineNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<Dictionary<long, List<Dictionary<string, object?>>>> LoadRfqAttachmentGroupsAsync(
        IReadOnlyCollection<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        return _dbContext.RfqAttachments.AsNoTracking()
            .Where(att => att.LineItemId != null && lineItemIds.Contains(att.LineItemId.Value))
            .GroupBy(att => att.LineItemId!.Value)
            .ToDictionaryAsync(
                g => (long)g.Key,
                g => g.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList(),
                cancellationToken);
    }

    public Task<List<LineItemExportRecordRow>> LoadLineItemExportRecordsAsync(
        long rfqId,
        IReadOnlyCollection<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        return (from li in _dbContext.RfqLineItems.AsNoTracking()
                join q in _dbContext.Quotes.AsNoTracking()
                    on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                from q in quoteGroup.DefaultIfEmpty()
                where li.RfqId == rfqId && lineItemIds.Contains(li.Id)
                select new LineItemExportRecordRow
                {
                    Id = li.Id,
                    LineNumber = li.LineNumber,
                    Status = li.Status,
                    SelectedQuoteId = li.SelectedQuoteId,
                    QuoteStatus = q != null ? q.Status : null,
                })
            .ToListAsync(cancellationToken);
    }

    public Task<List<long>> LoadCompletedLineItemIdsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems.AsNoTracking()
            .Where(li => li.RfqId == rfqId && li.Status == "completed")
            .Select(li => li.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<RfqLineItem>> LoadRfqLineItemsByIdsAsync(
        long rfqId,
        IReadOnlyCollection<long> lineItemIds,
        CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems
            .Where(li => li.RfqId == rfqId && lineItemIds.Contains(li.Id))
            .ToListAsync(cancellationToken);
    }

    public IQueryable<SupplierInvitationListRow> QuerySupplierInvitationRows(int supplierId)
    {
        return from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
               join rfq in _dbContext.Rfqs.AsNoTracking() on (long)inv.RfqId equals rfq.Id
               let latestRound = _dbContext.RfqBidRounds.AsNoTracking()
                   .Where(round => round.RfqId == inv.RfqId)
                   .OrderByDescending(round => round.RoundNumber)
                   .ThenByDescending(round => round.Id)
                   .FirstOrDefault()
               let quote = _dbContext.Quotes.AsNoTracking()
                   .Where(q => q.SupplierId == supplierId &&
                               q.RfqId == inv.RfqId &&
                               q.IsLatest &&
                               ((latestRound == null && q.BidRoundId == null) ||
                                (latestRound != null && q.BidRoundId == latestRound.Id)))
                   .OrderByDescending(q => q.SubmittedAt)
                   .ThenByDescending(q => q.Id)
                   .FirstOrDefault()
               where inv.SupplierId == supplierId
                     && ((latestRound == null && inv.BidRoundId == null) ||
                         (latestRound != null && inv.BidRoundId == latestRound.Id))
                select new SupplierInvitationListRow
                {
                    Rfq = rfq,
                    Invitation = inv,
                    BidRound = latestRound,
                    QuoteId = quote == null ? null : quote.Id,
                    QuoteStatus = quote == null ? null : quote.Status,
                    QuoteSubmittedAt = quote == null ? null : quote.SubmittedAt,
                };
    }

    public Task<QuoteVisibilityResult> GetQuoteVisibilityAsync(long rfqId, SupplierSystem.Application.Models.Auth.AuthUser user, CancellationToken cancellationToken)
    {
        return QuoteVisibility.GetVisibilityAsync(_dbContext, (int)rfqId, user, cancellationToken);
    }

    public async Task<List<string>> LoadSupplierEmailsForRfqAsync(int rfqId, CancellationToken cancellationToken)
    {
        var supplierEmails = await (from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
                                    join supplier in _dbContext.Suppliers.AsNoTracking()
                                        on inv.SupplierId equals supplier.Id
                                    where inv.RfqId == rfqId
                                    select supplier.ContactEmail)
            .ToListAsync(cancellationToken);

        var invitationEmails = await _dbContext.SupplierRfqInvitations.AsNoTracking()
            .Where(inv => inv.RfqId == rfqId && inv.RecipientEmail != null)
            .Select(inv => inv.RecipientEmail!)
            .ToListAsync(cancellationToken);

        var externalEmails = await _dbContext.RfqExternalInvitations.AsNoTracking()
            .Where(inv => inv.RfqId == rfqId)
            .Select(inv => inv.Email)
            .ToListAsync(cancellationToken);

        return supplierEmails.Concat(invitationEmails).Concat(externalEmails).ToList();
    }

    public Task<List<string>> LoadUserEmailsByRoleAsync(string normalizedRole, CancellationToken cancellationToken)
    {
        return _dbContext.Users.AsNoTracking()
            .Where(user => user.Role != null &&
                           user.Email != null &&
                           user.Role.ToLower() == normalizedRole)
            .Select(user => user.Email!)
            .ToListAsync(cancellationToken);
    }

    public Task<SystemConfig?> FindSystemConfigAsync(string key, CancellationToken cancellationToken)
    {
        return _dbContext.SystemConfigs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == key, cancellationToken);
    }

    public Task<RfqPrRecord?> FindRfqPrRecordAsync(long rfqId, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.RfqPrRecords.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(pr => pr.RfqId == rfqId, cancellationToken);
    }

    public void AddRfqPrRecord(RfqPrRecord record)
    {
        _dbContext.RfqPrRecords.Add(record);
    }

    public void AddRfq(SupplierSystem.Domain.Entities.Rfq rfq)
    {
        _dbContext.Rfqs.Add(rfq);
    }

    public void AddRfqLineItem(RfqLineItem lineItem)
    {
        _dbContext.RfqLineItems.Add(lineItem);
    }

    public Task<List<RfqLineItem>> LoadRfqLineItemsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqLineItems
            .Where(li => li.RfqId == rfqId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveRfqLineItems(IEnumerable<RfqLineItem> lineItems)
    {
        _dbContext.RfqLineItems.RemoveRange(lineItems);
    }

    public void AddSupplierInvitation(SupplierRfqInvitation invitation)
    {
        _dbContext.SupplierRfqInvitations.Add(invitation);
    }

    public Task<List<SupplierRfqInvitation>> LoadSupplierInvitationsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return _dbContext.SupplierRfqInvitations
            .Where(inv => inv.RfqId == rfqId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveSupplierInvitations(IEnumerable<SupplierRfqInvitation> invitations)
    {
        _dbContext.SupplierRfqInvitations.RemoveRange(invitations);
    }

    public void AddExternalInvitation(RfqExternalInvitation invitation)
    {
        _dbContext.RfqExternalInvitations.Add(invitation);
    }

    public Task<List<RfqExternalInvitation>> LoadExternalInvitationsAsync(long rfqId, CancellationToken cancellationToken)
    {
        return LoadExternalInvitationsAsync(rfqId, bidRoundId: null, cancellationToken);
    }

    public Task<List<RfqExternalInvitation>> LoadExternalInvitationsAsync(long rfqId, long? bidRoundId, CancellationToken cancellationToken)
    {
        return _dbContext.RfqExternalInvitations
            .Where(inv => inv.RfqId == rfqId &&
                          (bidRoundId == null ? inv.BidRoundId == null : inv.BidRoundId == bidRoundId))
            .OrderByDescending(inv => inv.InvitedAt)
            .ToListAsync(cancellationToken);
    }

    public void RemoveExternalInvitations(IEnumerable<RfqExternalInvitation> invitations)
    {
        _dbContext.RfqExternalInvitations.RemoveRange(invitations);
    }

    public void RemoveRfq(SupplierSystem.Domain.Entities.Rfq rfq)
    {
        _dbContext.Rfqs.Remove(rfq);
    }

    public void AddApprovalComment(ApprovalComment comment)
    {
        _dbContext.ApprovalComments.Add(comment);
    }

    public void AddRfqReview(RfqReview review)
    {
        _dbContext.RfqReviews.Add(review);
    }

    public void AddPriceComparisonAttachment(PriceComparisonAttachment attachment)
    {
        _dbContext.PriceComparisonAttachments.Add(attachment);
    }

    public void AddRfqApproval(RfqApproval approval)
    {
        _dbContext.RfqApprovals.Add(approval);
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

public sealed class RfqLineItemQuantityRecord
{
    public long Id { get; init; }
    public decimal Quantity { get; init; }
}

public sealed class QuoteListRow
{
    public required Quote Quote { get; init; }
    public string? CompanyName { get; init; }
    public string? CompanyId { get; init; }
    public string? Stage { get; init; }
}

public sealed class RfqInvitedSupplierRow
{
    public int Id { get; init; }
    public string? CompanyId { get; init; }
    public string? SupplierCode { get; init; }
    public string? VendorCode { get; init; }
    public string? CompanyName { get; init; }
    public string? Stage { get; init; }
    public string? InvitationStatus { get; init; }
    public string? QuoteStatus { get; init; }
    public string? QuoteSubmittedAt { get; init; }
}

public sealed class SupplierInvitationListRow
{
    public required SupplierSystem.Domain.Entities.Rfq Rfq { get; init; }
    public required SupplierRfqInvitation Invitation { get; init; }
    public RfqBidRound? BidRound { get; init; }
    public long? QuoteId { get; init; }
    public string? QuoteStatus { get; init; }
    public string? QuoteSubmittedAt { get; init; }
}

public sealed class QuoteLineItemWithQuantityRow
{
    public required QuoteLineItem QuoteLineItem { get; init; }
    public decimal? RfqQuantity { get; init; }
}

public sealed class LineItemExportRecordRow
{
    public long Id { get; init; }
    public int? LineNumber { get; init; }
    public string? Status { get; init; }
    public long? SelectedQuoteId { get; init; }
    public string? QuoteStatus { get; init; }
}
