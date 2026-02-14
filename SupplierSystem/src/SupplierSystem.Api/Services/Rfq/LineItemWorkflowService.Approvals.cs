using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Rfq;

public sealed partial class LineItemWorkflowService
{
    public async Task<Dictionary<string, object?>> DirectorApproveAsync(
        int rfqId,
        int lineItemId,
        string decision,
        string? comments,
        int? newQuoteId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.ProcurementDirectorRfqApprove });

        if (!string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(decision, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationErrorException("Decision must be \"approved\" or \"rejected\"");
        }

        var lineItem = await _dbContext.RfqLineItems
            .FirstOrDefaultAsync(li => li.Id == lineItemId && li.RfqId == rfqId, cancellationToken);
        if (lineItem == null)
        {
            throw new Exception($"Line item with id {lineItemId} not found");
        }

        if (!string.Equals(lineItem.Status, LineItemStatus.PendingDirector, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Line item is not pending director approval");
        }

        var quoteChangeReason = (string?)null;
        if (newQuoteId.HasValue && newQuoteId.Value != lineItem.SelectedQuoteId)
        {
            var newQuote = await _dbContext.Quotes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == newQuoteId.Value && q.RfqId == rfqId, cancellationToken);
            if (newQuote == null)
            {
                throw new Exception($"New quote with id {newQuoteId.Value} not found");
            }
            quoteChangeReason = "Director changed selected quote during approval";
        }

        var now = DateTime.UtcNow.ToString("o");
        var newStatus = string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase)
            ? LineItemStatus.PendingPo
            : LineItemStatus.Draft;
        var approverRole = string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase)
            ? "purchaser"
            : null;
        var reason = string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase)
            ? "Director approved, ready for PO"
            : comments ?? "Director rejected line item";

        var previousQuoteId = lineItem.SelectedQuoteId;
        var effectiveQuoteId = newQuoteId ?? previousQuoteId;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await TransitionLineItemAsync(
            lineItem,
            newStatus,
            user,
            reason,
            new Dictionary<string, object?>
            {
                ["current_approver_role"] = approverRole,
                ["selected_quote_id"] = effectiveQuoteId,
            },
            cancellationToken);

        if (newQuoteId.HasValue && newQuoteId.Value != previousQuoteId)
        {
            var newQuote = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == newQuoteId.Value, cancellationToken);
            if (newQuote != null)
            {
                newQuote.Status = "selected";
                newQuote.UpdatedAt = now;
            }

            if (previousQuoteId.HasValue)
            {
                var stillUsed = await _dbContext.RfqLineItems.AsNoTracking()
                    .CountAsync(li => li.SelectedQuoteId == previousQuoteId && li.Id != lineItemId, cancellationToken);
                if (stillUsed == 0)
                {
                    var oldQuote = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == previousQuoteId.Value, cancellationToken);
                    if (oldQuote != null)
                    {
                        oldQuote.Status = "submitted";
                        oldQuote.UpdatedAt = now;
                    }
                }
            }
        }

        _dbContext.RfqLineItemApprovalHistories.Add(new RfqLineItemApprovalHistory
        {
            RfqLineItemId = lineItemId,
            Step = "director",
            ApproverId = user.Id,
            ApproverName = user.Name,
            ApproverRole = user.Role,
            Decision = decision,
            Comments = string.IsNullOrWhiteSpace(comments) ? null : comments,
            PreviousQuoteId = newQuoteId.HasValue ? previousQuoteId : null,
            NewQuoteId = newQuoteId,
            ChangeReason = quoteChangeReason,
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await LogAuditAsync("rfq_line_item", lineItemId.ToString(), $"director_{decision}",
            new { decision, comments }, user, cancellationToken);

        await _priceAuditService.UpdateApprovalForLineItemAsync(lineItemId, decision, comments, now, cancellationToken);
        await _priceAuditService.SyncSelectedQuoteForLineItemAsync(lineItemId, effectiveQuoteId, cancellationToken);

        return NodeCaseMapper.ToSnakeCaseDictionary(lineItem);
    }

}
