using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Services.Rfq;

public sealed partial class LineItemWorkflowService
{
    public async Task<List<Dictionary<string, object?>>> GetApprovalHistoryAsync(
        int lineItemId,
        CancellationToken cancellationToken)
    {
        var history = await _dbContext.RfqLineItemApprovalHistories.AsNoTracking()
            .Where(h => h.RfqLineItemId == lineItemId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return history.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();
    }

    public async Task<List<Dictionary<string, object?>>> GetPendingApprovalsAsync(
        string role,
        AuthUser user,
        string? status,
        CancellationToken cancellationToken)
    {
        _ = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
            ? "completed"
            : "pending";

        List<string> statusFilters;
        switch (role)
        {
            case "purchaser":
                statusFilters = new List<string> { LineItemStatus.PendingPo, LineItemStatus.Draft };
                break;
            case "procurement_manager":
                statusFilters = new List<string>();
                break;
            case "procurement_director":
                statusFilters = new List<string> { LineItemStatus.PendingDirector };
                break;
            case "department_user":
                statusFilters = new List<string>();
                break;
            default:
                statusFilters = new List<string>();
                break;
        }

        if (statusFilters.Count == 0)
        {
            return new List<Dictionary<string, object?>>();
        }

        if (role == "purchaser")
        {
            var records = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                                 join rfq in _dbContext.Rfqs.AsNoTracking() on li.RfqId equals rfq.Id
                                 join q in _dbContext.Quotes.AsNoTracking() on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                                 from q in quoteGroup.DefaultIfEmpty()
                                 join s in _dbContext.Suppliers.AsNoTracking() on q.SupplierId equals s.Id into supplierGroup
                                 from s in supplierGroup.DefaultIfEmpty()
                                 where rfq.CreatedBy == user.Id &&
                                       (li.Status == LineItemStatus.PendingPo ||
                                        (li.Status == LineItemStatus.Draft && li.SelectedQuoteId != null))
                                 orderby li.UpdatedAt descending
                                 select new
                                 {
                                     li.Id,
                                     li.RfqId,
                                     li.LineNumber,
                                     li.MaterialCategory,
                                     li.Brand,
                                     li.ItemName,
                                     li.Specifications,
                                     li.Quantity,
                                     li.Unit,
                                     li.EstimatedUnitPrice,
                                     li.Currency,
                                     li.Parameters,
                                     li.Notes,
                                     li.CreatedAt,
                                     li.Status,
                                     li.CurrentApproverRole,
                                     li.SelectedQuoteId,
                                     li.PoId,
                                     li.UpdatedAt,
                                     RfqTitle = rfq.Title,
                                     RfqCreatedBy = rfq.CreatedBy,
                                     RequestingDepartment = rfq.RequestingDepartment,
                                     SupplierId = q != null ? (int?)q.SupplierId : null,
                                     QuoteAmount = q != null ? q.TotalAmount : null,
                                     SupplierName = s != null ? s.CompanyName : null,
                                 })
                .ToListAsync(cancellationToken);

            var recordDicts = records.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();
            return recordDicts
                .Select(record => (Dictionary<string, object?>)CaseTransform.ToCamelCase(record)!)
                .ToList();
        }

        var statusSet = new HashSet<string>(statusFilters, StringComparer.OrdinalIgnoreCase);

        var genericRecords = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                                    join rfq in _dbContext.Rfqs.AsNoTracking() on li.RfqId equals rfq.Id
                                    join q in _dbContext.Quotes.AsNoTracking() on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                                    from q in quoteGroup.DefaultIfEmpty()
                                    join s in _dbContext.Suppliers.AsNoTracking() on q.SupplierId equals s.Id into supplierGroup
                                    from s in supplierGroup.DefaultIfEmpty()
                                    where statusSet.Contains(li.Status ?? string.Empty)
                                    orderby li.UpdatedAt
                                    select new
                                    {
                                        li.Id,
                                        li.RfqId,
                                        li.LineNumber,
                                        li.MaterialCategory,
                                        li.Brand,
                                        li.ItemName,
                                        li.Specifications,
                                        li.Quantity,
                                        li.Unit,
                                        li.EstimatedUnitPrice,
                                        li.Currency,
                                        li.Parameters,
                                        li.Notes,
                                        li.CreatedAt,
                                        li.Status,
                                        li.CurrentApproverRole,
                                        li.SelectedQuoteId,
                                        li.PoId,
                                        li.UpdatedAt,
                                        RfqTitle = rfq.Title,
                                        RfqCreatedBy = rfq.CreatedBy,
                                        SupplierId = q != null ? (int?)q.SupplierId : null,
                                        QuoteAmount = q != null ? q.TotalAmount : null,
                                        SupplierName = s != null ? s.CompanyName : null,
                                    })
            .ToListAsync(cancellationToken);

        var genericDicts = genericRecords.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();
        return genericDicts
            .Select(record => (Dictionary<string, object?>)CaseTransform.ToCamelCase(record)!)
            .ToList();
    }

    public async Task InvitePurchasersAsync(
        int rfqId,
        int lineItemId,
        List<int> purchaserIds,
        string? message,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        if (purchaserIds.Count == 0)
        {
            throw new ValidationErrorException("At least one purchaser must be selected",
                new { purchaserIds });
        }

        string requiredStatus;
        string permission;
        string approvalStep;

        if (string.Equals(user.Role, "procurement_director", StringComparison.OrdinalIgnoreCase))
        {
            permission = Permissions.ProcurementDirectorRfqApprove;
            requiredStatus = LineItemStatus.PendingDirector;
            approvalStep = "director";
        }
        else
        {
            throw new ServiceErrorException(403, "Only procurement director can invite purchasers");
        }

        RequirePermissions(user, new[] { permission });

        var lineItem = await _dbContext.RfqLineItems.AsNoTracking()
            .FirstOrDefaultAsync(li => li.Id == lineItemId && li.RfqId == rfqId, cancellationToken);
        if (lineItem == null)
        {
            throw new Exception($"Line item with id {lineItemId} not found");
        }

        if (!string.Equals(lineItem.Status, requiredStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Line item is not in the correct approval stage to invite purchasers");
        }

        var now = DateTime.UtcNow.ToString("o");
        var comment = string.IsNullOrWhiteSpace(message)
            ? $"@purchasers({string.Join(", ", purchaserIds)})"
            : $"@purchasers({string.Join(", ", purchaserIds)}): {message}";

        _dbContext.RfqLineItemApprovalHistories.Add(new RfqLineItemApprovalHistory
        {
            RfqLineItemId = lineItemId,
            Step = approvalStep,
            ApproverId = user.Id,
            ApproverName = user.Name,
            ApproverRole = user.Role,
            Decision = "invited",
            Comments = comment,
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync("rfq_line_item", lineItemId.ToString(), "invite_purchasers",
            new { purchaserIds, message }, user, cancellationToken);
    }
}
