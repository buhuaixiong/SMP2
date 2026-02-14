using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpGet("{id:int}/approvals")]
    public async Task<IActionResult> GetApprovals(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        try
        {
            var approvals = await _dbContext.RfqApprovals.AsNoTracking()
                .Where(a => a.RfqId == id)
                .OrderBy(a => a.StepOrder)
                .ToListAsync(cancellationToken);

            var approvalsWithComments = new List<Dictionary<string, object?>>();
            foreach (var approval in approvals)
            {
                var comments = await _dbContext.ApprovalComments.AsNoTracking()
                    .Where(c => c.ApprovalId == approval.Id)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync(cancellationToken);

                var approvalDict = NodeCaseMapper.ToSnakeCaseDictionary(approval);
                approvalDict["comments"] = comments.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();
                approvalsWithComments.Add(approvalDict);
            }

            return Ok(new { data = approvalsWithComments });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch approvals.");
            return StatusCode(500, new { message = "Failed to fetch approvals." });
        }
    }

    [HttpPost("{id:int}/approvals/{approvalId:int}/approve")]
    public async Task<IActionResult> Approve(int id, int approvalId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0 || approvalId <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ or approval ID." });
        }

        var decision = ReadStringValue(body, "decision");
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        try
        {
            var approval = await _dbContext.RfqApprovals
                .FirstOrDefaultAsync(a => a.Id == approvalId && a.RfqId == id, cancellationToken);
            if (approval == null)
            {
                return NotFound(new { message = "Approval not found." });
            }

            if (!string.Equals(approval.ApproverRole, user.Role, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { message = "You are not authorized to approve this step." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow.ToString("o");

            approval.Status = "approved";
            approval.ApproverId = user.Id;
            approval.Decision = string.IsNullOrWhiteSpace(decision) ? null : decision;
            approval.DecidedAt = now;

            var nextApproval = await _dbContext.RfqApprovals.AsNoTracking()
                .Where(a => a.RfqId == id && a.StepOrder > approval.StepOrder && a.Status == "pending")
                .OrderBy(a => a.StepOrder)
                .FirstOrDefaultAsync(cancellationToken);

            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq != null)
            {
                if (nextApproval != null)
                {
                    rfq.ApprovalStatus = "pending_director";
                    rfq.Status = "pending_director_approval";
                }
                else
                {
                    rfq.ApprovalStatus = "approved";
                    rfq.Status = "approved";
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _priceAuditService.UpdateApprovalForRfqAsync(id, rfq?.ApprovalStatus ?? approval.Status, approval.Decision, approval.DecidedAt, cancellationToken);

            await LogAuditAsync("rfq_approval", approvalId.ToString(CultureInfo.InvariantCulture), "approve",
                new { decision, rfqId = id },
                user,
                cancellationToken);

            if (rfq != null &&
                string.Equals(rfq.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                await TryNotifyRfqDirectorApprovedAsync(rfq, cancellationToken);
            }

            return Ok(new { message = "Approval successful" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve.");
            return StatusCode(500, new { message = "Failed to approve." });
        }
    }

    [HttpPost("{id:int}/approvals/{approvalId:int}/reject")]
    public async Task<IActionResult> Reject(int id, int approvalId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0 || approvalId <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ or approval ID." });
        }

        var decision = ReadStringValue(body, "decision");
        if (string.IsNullOrWhiteSpace(decision))
        {
            return BadRequest(new { message = "Rejection reason (decision) is required." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        try
        {
            var approval = await _dbContext.RfqApprovals
                .FirstOrDefaultAsync(a => a.Id == approvalId && a.RfqId == id, cancellationToken);
            if (approval == null)
            {
                return NotFound(new { message = "Approval not found." });
            }

            if (!string.Equals(approval.ApproverRole, user.Role, StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { message = "You are not authorized to reject this step." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow.ToString("o");

            approval.Status = "rejected";
            approval.ApproverId = user.Id;
            approval.Decision = decision;
            approval.DecidedAt = now;

            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq != null)
            {
                rfq.ApprovalStatus = "rejected";
                rfq.Status = "rejected";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await _priceAuditService.UpdateApprovalForRfqAsync(id, rfq?.ApprovalStatus ?? approval.Status, approval.Decision, approval.DecidedAt, cancellationToken);

            await LogAuditAsync("rfq_approval", approvalId.ToString(CultureInfo.InvariantCulture), "reject",
                new { decision, rfqId = id },
                user,
                cancellationToken);

            if (rfq != null &&
                string.Equals(rfq.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                await TryNotifyRfqDirectorRejectedAsync(rfq, cancellationToken);
            }

            return Ok(new { message = "RFQ rejected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject.");
            return StatusCode(500, new { message = "Failed to reject." });
        }
    }

    [HttpPost("{id:int}/approvals/{approvalId:int}/comments")]
    public async Task<IActionResult> AddApprovalComment(int id, int approvalId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (approvalId <= 0)
        {
            return BadRequest(new { message = "Invalid approval ID." });
        }

        var content = ReadStringValue(body, "content");
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest(new { message = "Comment content is required." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        try
        {
            var comment = new ApprovalComment
            {
                ApprovalId = approvalId,
                AuthorId = user.Id,
                AuthorName = user.Name,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow.ToString("o"),
            };

            _dbContext.ApprovalComments.Add(comment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(201, new
            {
                message = "Comment added successfully",
                data = NodeCaseMapper.ToSnakeCaseDictionary(comment),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add comment.");
            return StatusCode(500, new { message = "Failed to add comment." });
        }
    }

    [HttpPost("{id:int}/approvals/{approvalId:int}/invite-purchasers")]
    public async Task<IActionResult> InvitePurchasers(int id, int approvalId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0 || approvalId <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ or approval ID." });
        }

        if (!JsonHelper.TryGetProperty(body, "purchaserIds", out var idsElement) ||
            idsElement.ValueKind != JsonValueKind.Array ||
            idsElement.GetArrayLength() == 0)
        {
            return BadRequest(new { message = "At least one purchaser ID is required." });
        }

        var purchaserIds = new List<int>();
        foreach (var entry in idsElement.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var idValue))
            {
                purchaserIds.Add(idValue);
            }
        }

        if (purchaserIds.Count == 0)
        {
            return BadRequest(new { message = "At least one purchaser ID is required." });
        }

        var message = ReadStringValue(body, "message");
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        try
        {
            var approval = await _dbContext.RfqApprovals.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == approvalId && a.RfqId == id, cancellationToken);
            if (approval == null)
            {
                return NotFound(new { message = "Approval not found." });
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                _dbContext.ApprovalComments.Add(new ApprovalComment
                {
                    ApprovalId = approvalId,
                    AuthorId = user.Id,
                    AuthorName = user.Name,
                    Content = $"@Purchasers: {message.Trim()}",
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync("rfq_approval", approvalId.ToString(CultureInfo.InvariantCulture), "invite_purchasers",
                new { purchaserIds, message },
                user,
                cancellationToken);

            return Ok(new { message = "Purchasers invited successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invite purchasers.");
            return StatusCode(500, new { message = "Failed to invite purchasers." });
        }
    }
}
