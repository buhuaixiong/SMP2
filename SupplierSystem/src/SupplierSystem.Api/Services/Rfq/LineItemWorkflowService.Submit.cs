using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Rfq;

public sealed partial class LineItemWorkflowService
{
    public async Task<Dictionary<string, object?>> SubmitLineItemAsync(
        int rfqId,
        int lineItemId,
        int? selectedQuoteId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });
        ValidateRequired(new Dictionary<string, object?>
        {
            ["rfqId"] = rfqId,
            ["lineItemId"] = lineItemId,
            ["selectedQuoteId"] = selectedQuoteId,
        }, new[] { "rfqId", "lineItemId", "selectedQuoteId" });

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq == null)
        {
            throw new Exception($"RFQ with id {rfqId} not found");
        }

        if (!string.Equals(rfq.CreatedBy, user.Id, StringComparison.Ordinal))
        {
            throw new ServiceErrorException(403, "Only RFQ creator can submit line items for approval");
        }

        var lineItem = await _dbContext.RfqLineItems
            .FirstOrDefaultAsync(li => li.Id == lineItemId && li.RfqId == rfqId, cancellationToken);
        if (lineItem == null)
        {
            throw new Exception($"Line item with id {lineItemId} not found");
        }

        if (!string.Equals(lineItem.Status, LineItemStatus.Draft, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(lineItem.Status, LineItemStatus.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Line item must be in draft or rejected status to submit");
        }

        var quoteId = selectedQuoteId ?? 0;
        var quote = await _dbContext.Quotes
            .FirstOrDefaultAsync(q => q.Id == quoteId && q.RfqId == rfqId, cancellationToken);
        if (quote == null)
        {
            throw new Exception($"Quote with id {quoteId} not found");
        }

        var oldSelectedQuoteId = lineItem.SelectedQuoteId;
        var now = DateTime.UtcNow.ToString("o");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await TransitionLineItemAsync(
            lineItem,
            LineItemStatus.PendingDirector,
            user,
            "Line item submitted directly to director for approval",
            new Dictionary<string, object?>
            {
                ["current_approver_role"] = "procurement_director",
                ["selected_quote_id"] = quoteId,
            },
            cancellationToken);

        quote.Status = "selected";
        quote.UpdatedAt = now;

        if (oldSelectedQuoteId.HasValue && oldSelectedQuoteId.Value != quoteId)
        {
            var stillUsed = await _dbContext.RfqLineItems.AsNoTracking()
                .CountAsync(li => li.SelectedQuoteId == oldSelectedQuoteId && li.Id != lineItemId, cancellationToken);
            if (stillUsed == 0)
            {
                var oldQuote = await _dbContext.Quotes
                    .FirstOrDefaultAsync(q => q.Id == oldSelectedQuoteId.Value, cancellationToken);
                if (oldQuote != null)
                {
                    oldQuote.Status = "submitted";
                    oldQuote.UpdatedAt = now;
                }
            }
        }

        _dbContext.RfqLineItemApprovalHistories.Add(new RfqLineItemApprovalHistory
        {
            RfqLineItemId = lineItemId,
            Step = "submitted_to_director",
            ApproverId = user.Id,
            ApproverName = user.Name,
            ApproverRole = user.Role,
            Decision = "submitted",
            Comments = "Line item submitted directly to director",
            NewQuoteId = quoteId,
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await LogAuditAsync("rfq_line_item", lineItemId.ToString(), "submit_for_approval",
            new { rfqId, selectedQuoteId = quoteId }, user, cancellationToken);

        await _priceAuditService.SyncSelectedQuoteForLineItemAsync(lineItem.Id, quoteId, cancellationToken);

        return NodeCaseMapper.ToSnakeCaseDictionary(lineItem);
    }
}
