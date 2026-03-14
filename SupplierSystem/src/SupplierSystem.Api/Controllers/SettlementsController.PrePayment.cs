using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class SettlementsController
{
    [HttpPost("pre-payment/{id:int}/review")]
    public async Task<IActionResult> ReviewPrePayment(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var status = (ReadString(body, "status") ?? string.Empty).Trim().ToLowerInvariant();
        var reviewNotes = ReadString(body, "review_notes", "reviewNotes");
        var rejectionReason = ReadString(body, "rejection_reason", "rejectionReason");

        if (status != "approved" && status != "rejected")
        {
            return BadRequest(new { message = "status must be approved or rejected." });
        }

        var settlement = await _settlementStore.FindSettlementByIdAndTypeAsync(id, "pre_payment", cancellationToken);

        if (settlement == null)
        {
            return NotFound(new { message = "Pre-payment settlement not found." });
        }

        if (!settlement.SupplierId.HasValue)
        {
            return BadRequest(new { message = "Settlement supplier is missing." });
        }

        var supplier = await _settlementStore.FindSupplierAsync(settlement.SupplierId.Value, cancellationToken);

        if (supplier == null)
        {
            return BadRequest(new { message = "Supplier not found." });
        }

        if (!string.Equals(supplier.Stage, "temporary", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only temporary suppliers can request pre-payment." });
        }

        var rfqAmount = await GetRfqAmountAsync(settlement.RfqId, cancellationToken);
        if (!rfqAmount.HasValue || rfqAmount.Value <= 0)
        {
            return BadRequest(new { message = "RFQ amount is required for pre-payment review." });
        }

        var settlementAmount = GetSettlementAmount(settlement);
        var prePaymentRatio = settlementAmount == 0 ? 0 : settlementAmount / rfqAmount.Value * 100m;
        if (prePaymentRatio > 30m)
        {
            return BadRequest(new
            {
                message = $"Pre-payment ratio {prePaymentRatio:0.##}% exceeds the 30% threshold.",
                current_ratio = prePaymentRatio
            });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var overdueCount = await _settlementStore.CountApprovedOverdueSettlementsAsync(
            settlement.SupplierId.Value,
            now,
            cancellationToken);

        if (overdueCount > 0)
        {
            return BadRequest(new
            {
                message = "Supplier has overdue settlements; pre-payment cannot be approved.",
                overdue_count = overdueCount
            });
        }

        settlement.Status = status;
        settlement.ReviewerId = TryParseUserId(user);
        settlement.ReviewedAt = now;
        settlement.ReviewNotes = reviewNotes;
        settlement.RejectionReason = rejectionReason;

        await _settlementStore.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(user, $"pre_payment_review_{status}", settlement.Id.ToString(), new
        {
            pre_payment_ratio = prePaymentRatio,
            review_notes = reviewNotes,
            rejection_reason = rejectionReason
        });

        return Ok(new
        {
            message = "Pre-payment review completed.",
            status,
            pre_payment_ratio = prePaymentRatio
        });
    }

    [HttpPost("pre-payment/{id:int}/director-approval")]
    public async Task<IActionResult> DirectorApproval(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_director", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var approvalStatus = ReadBool(body, "approval_status", "approvalStatus");
        var approvalNotes = ReadString(body, "approval_notes", "approvalNotes");
        var exceptionalReason = ReadString(body, "exceptional_reason", "exceptionalReason");

        if (!approvalStatus.HasValue)
        {
            return BadRequest(new { message = "approval_status is required." });
        }

        if (string.IsNullOrWhiteSpace(exceptionalReason))
        {
            return BadRequest(new { message = "exceptional_reason is required for director approval." });
        }

        var settlement = await _settlementStore.FindSettlementByIdAndTypeAsync(id, "pre_payment", cancellationToken);

        if (settlement == null)
        {
            return NotFound(new { message = "Pre-payment settlement not found." });
        }

        if (!settlement.SupplierId.HasValue)
        {
            return BadRequest(new { message = "Settlement supplier is missing." });
        }

        var supplier = await _settlementStore.FindSupplierAsync(settlement.SupplierId.Value, cancellationToken);

        if (supplier == null)
        {
            return BadRequest(new { message = "Supplier not found." });
        }

        var rfqAmount = await GetRfqAmountAsync(settlement.RfqId, cancellationToken);
        if (!rfqAmount.HasValue || rfqAmount.Value <= 0)
        {
            return BadRequest(new { message = "RFQ amount is required for director approval." });
        }

        var settlementAmount = GetSettlementAmount(settlement);
        var prePaymentRatio = settlementAmount == 0 ? 0 : settlementAmount / rfqAmount.Value * 100m;
        var isExceptional = prePaymentRatio > 30m ||
                            (string.Equals(supplier.Stage, "formal", StringComparison.OrdinalIgnoreCase)
                             && prePaymentRatio > 50m);

        if (!isExceptional)
        {
            return BadRequest(new
            {
                message = "This pre-payment does not require exceptional approval.",
                current_ratio = prePaymentRatio
            });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        settlement.DirectorApproved = approvalStatus.Value;
        settlement.DirectorApproverId = TryParseUserId(user);
        settlement.DirectorApprovedAt = now;
        settlement.DirectorApprovalNotes = approvalNotes;
        settlement.ExceptionalReason = exceptionalReason;

        await _settlementStore.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(user, $"exceptional_pre_payment_approval_{approvalStatus.Value}", settlement.Id.ToString(), new
        {
            pre_payment_ratio = prePaymentRatio,
            approval_notes = approvalNotes,
            exceptional_reason = exceptionalReason
        });

        return Ok(new
        {
            message = "Exceptional pre-payment approval completed.",
            approval_status = approvalStatus.Value,
            pre_payment_ratio = prePaymentRatio
        });
    }

    private static int? TryParseUserId(SupplierSystem.Application.Models.Auth.AuthUser? user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.Id))
        {
            return null;
        }

        return int.TryParse(user.Id, out var parsed) ? parsed : null;
    }

    private static decimal GetSettlementAmount(Settlement settlement)
    {
        if (settlement.TotalAmount.HasValue)
        {
            return settlement.TotalAmount.Value;
        }

        if (settlement.GrandTotal.HasValue)
        {
            return settlement.GrandTotal.Value;
        }

        return 0m;
    }

    private async Task<decimal?> GetRfqAmountAsync(int? rfqId, CancellationToken cancellationToken)
    {
        if (!rfqId.HasValue)
        {
            return null;
        }

        return await _settlementStore.FindRfqAmountAsync(rfqId.Value, cancellationToken);
    }
}
