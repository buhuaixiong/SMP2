using System.Text.Json;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Rfq;

public sealed partial class LineItemWorkflowService
{
    private async Task TransitionLineItemAsync(
        RfqLineItem lineItem,
        string newStatus,
        AuthUser user,
        string reason,
        Dictionary<string, object?> extraPatch,
        CancellationToken cancellationToken)
    {
        var oldStatus = lineItem.Status;
        lineItem.Status = newStatus;
        lineItem.UpdatedAt = DateTime.UtcNow.ToString("o");

        if (extraPatch.TryGetValue("current_approver_role", out var approver))
        {
            lineItem.CurrentApproverRole = approver?.ToString();
        }

        if (extraPatch.TryGetValue("selected_quote_id", out var quoteId))
        {
            lineItem.SelectedQuoteId = quoteId == null ? null : Convert.ToInt64(quoteId);
        }

        if (extraPatch.TryGetValue("po_id", out var poId))
        {
            lineItem.PoId = poId == null ? null : Convert.ToInt64(poId);
        }

        var historyLineItemId = checked((int)lineItem.Id);
        await RecordStatusHistoryAsync(historyLineItemId, oldStatus, newStatus, user, reason, cancellationToken);
    }

    private async Task RecordStatusHistoryAsync(
        int lineItemId,
        string? fromStatus,
        string toStatus,
        AuthUser user,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.LineItemStatusHistories.Add(new LineItemStatusHistory
            {
                LineItemId = lineItemId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ChangedBy = user.Id,
                ChangedAt = DateTime.UtcNow.ToString("o"),
                Reason = reason,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (!ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "Failed to record line item status history.");
            }
        }
    }

    private async Task LogAuditAsync(
        string entityType,
        string entityId,
        string action,
        object? changes,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = new SupplierSystem.Application.Models.Audit.AuditEntry
            {
                ActorId = user.Id,
                ActorName = user.Name,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Changes = changes == null ? null : JsonSerializer.Serialize(changes),
            };

            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry for {EntityType} {EntityId}", entityType, entityId);
        }
    }
}
