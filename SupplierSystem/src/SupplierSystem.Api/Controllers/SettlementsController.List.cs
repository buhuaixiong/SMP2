using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Controllers;

public sealed partial class SettlementsController
{
    [HttpGet]
    public async Task<IActionResult> ListSettlements(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "finance_director", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var type = Request.Query["type"].ToString();
        var status = Request.Query["status"].ToString();
        var page = ParseInt(Request.Query["page"], 1);
        var limit = ParseInt(Request.Query["limit"], 20);
        if (!TryReadIntFromQuery(Request.Query, out var supplierId, "supplier_id", "supplierId"))
        {
            return BadRequest(new { message = "supplierId must be a valid integer." });
        }

        page = Math.Max(1, page);
        limit = Math.Min(Math.Max(1, limit), 100);

        var role = ControllerHelpers.NormalizeRole(user?.Role) ?? string.Empty;

        var query = _settlementStore.QuerySettlementListItems();

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(item => item.Settlement.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Settlement.Status == status);
        }

        if (supplierId.HasValue)
        {
            query = query.Where(item => item.Settlement.SupplierId == supplierId.Value);
        }

        if (string.Equals(role, "finance_accountant", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item =>
                item.Settlement.Status != null && AccountantStatuses.Contains(item.Settlement.Status));
        }

        var settlements = await query
            .OrderByDescending(item => item.Settlement.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(item => new
            {
                item.Settlement.Id,
                item.Settlement.StatementNumber,
                item.Settlement.SupplierId,
                item.Settlement.RfqId,
                item.Settlement.Type,
                item.Settlement.PeriodStart,
                item.Settlement.PeriodEnd,
                item.Settlement.TotalInvoices,
                item.Settlement.TotalAmount,
                item.Settlement.TaxAmount,
                item.Settlement.GrandTotal,
                item.Settlement.Status,
                item.Settlement.CreatedBy,
                item.Settlement.CreatedAt,
                item.Settlement.ReviewerId,
                item.Settlement.ReviewedAt,
                item.Settlement.ReviewNotes,
                item.Settlement.RejectionReason,
                item.Settlement.DirectorApproved,
                item.Settlement.DirectorApproverId,
                item.Settlement.DirectorApprovedAt,
                item.Settlement.DirectorApprovalNotes,
                item.Settlement.ExceptionalReason,
                item.Settlement.PaymentDueDate,
                item.Settlement.PaidDate,
                item.Settlement.DisputeReceived,
                item.Settlement.DisputeReason,
                item.Settlement.DisputedItems,
                item.Settlement.SupportingDocuments,
                item.Settlement.DisputeProcessorId,
                item.Settlement.DisputeReceivedAt,
                item.Settlement.Details,
                item.SupplierName,
                item.SupplierStage,
                item.CreatorName,
                item.RfqTitle
            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = settlements });
    }

    [HttpGet("progress-tracking")]
    public async Task<IActionResult> GetProgressTracking(CancellationToken cancellationToken)
    {
        var permissionResult = RequireRole(HttpContext.GetAuthUser(), "finance_accountant", "finance_director", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var nowText = DateTimeOffset.UtcNow.ToString("o");
        var threshold = DateTime.UtcNow.AddDays(-30);

        var progressTracking = await _settlementStore.GetProgressTrackingAsync(threshold, nowText, cancellationToken);

        var completionRate = progressTracking.Total == 0
            ? 0
            : Math.Round((progressTracking.PaidCount + progressTracking.ArchivedCount) / (double)progressTracking.Total * 100, 2, MidpointRounding.AwayFromZero);

        return Ok(new
        {
            data = new
            {
                total_settlements = progressTracking.Total,
                draft_count = progressTracking.DraftCount,
                pending_approval_count = progressTracking.PendingApprovalCount,
                approved_count = progressTracking.ApprovedCount,
                paid_count = progressTracking.PaidCount,
                archived_count = progressTracking.ArchivedCount,
                pre_payment_count = progressTracking.PrePaymentCount,
                monthly_count = progressTracking.MonthlyCount,
                quarterly_count = progressTracking.QuarterlyCount,
                overdue_count = progressTracking.OverdueCount,
                settlement_completion_rate = completionRate
            }
        });
    }
}
