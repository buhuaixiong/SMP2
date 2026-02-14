using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Models.Requisitions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Security;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RequisitionsController
{
    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> SubmitRequisition(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.DepartmentRequisitionManage,
            Permissions.RfqCreate);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var requisition = await _dbContext.MaterialRequisitions
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition == null)
        {
            return NotFound(new { message = "Requisition not found." });
        }

        if (!string.Equals(requisition.RequestingPersonId, user.Id, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { message = "Only the requester can submit this requisition." });
        }

        if (!string.Equals(requisition.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only draft requisitions can be submitted." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        requisition.Status = "submitted";
        requisition.SubmittedAt = now;
        requisition.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "material_requisition",
            requisition.Id.ToString(),
            "submit",
            new { status = "submitted" },
            user,
            cancellationToken);

        return Ok(new { message = "Requisition submitted successfully." });
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApproveRequisition(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var requisition = await _dbContext.MaterialRequisitions
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition == null)
        {
            return NotFound(new { message = "Requisition not found." });
        }

        if (!string.Equals(requisition.Status, "submitted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only submitted requisitions can be approved." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        requisition.Status = "approved";
        requisition.ApprovedById = user.Id;
        requisition.ApprovedByName = user.Name;
        requisition.ApprovedAt = now;
        requisition.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "material_requisition",
            requisition.Id.ToString(),
            "approve",
            new { status = "approved" },
            user,
            cancellationToken);

        return Ok(new { message = "Requisition approved successfully." });
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> RejectRequisition(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var rejectionReason = RequisitionPayloadParser.GetString(body, "rejection_reason", "rejectionReason");
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return BadRequest(new { message = "Rejection reason is required." });
        }

        var requisition = await _dbContext.MaterialRequisitions
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition == null)
        {
            return NotFound(new { message = "Requisition not found." });
        }

        if (!string.Equals(requisition.Status, "submitted", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only submitted requisitions can be rejected." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        requisition.Status = "rejected";
        requisition.RejectedById = user.Id;
        requisition.RejectedByName = user.Name;
        requisition.RejectedAt = now;
        requisition.RejectionReason = rejectionReason;
        requisition.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "material_requisition",
            requisition.Id.ToString(),
            "reject",
            new { status = "rejected", rejection_reason = rejectionReason },
            user,
            cancellationToken);

        return Ok(new { message = "Requisition rejected." });
    }

    [HttpPost("{id:int}/items/{itemId:int}/convert-to-rfq")]
    public async Task<IActionResult> ConvertItemToRfq(int id, int itemId, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var requisition = await _dbContext.MaterialRequisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition == null)
        {
            return NotFound(new { message = "Requisition not found." });
        }

        if (!string.Equals(requisition.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only approved requisitions can be converted to RFQ." });
        }

        var item = await _dbContext.MaterialRequisitionItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId && i.RequisitionId == id, cancellationToken);

        if (item == null)
        {
            return NotFound(new { message = "Material item not found." });
        }

        if (item.ConvertedToRfqId.HasValue)
        {
            return BadRequest(new
            {
                message = "Item has already been converted to RFQ.",
                rfq_id = item.ConvertedToRfqId,
            });
        }

        return Ok(new
        {
            data = new { requisition, item },
            message = "Material item ready for RFQ conversion. Use this data to create an RFQ.",
        });
    }

    [HttpPost("{id:int}/items/{itemId:int}/mark-converted")]
    public async Task<IActionResult> MarkItemConverted(int id, int itemId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var rfqId = RequisitionPayloadParser.GetInt(body, "rfq_id", "rfqId");
        if (rfqId <= 0)
        {
            return BadRequest(new { message = "RFQ ID is required." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var item = await _dbContext.MaterialRequisitionItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.RequisitionId == id, cancellationToken);

        if (item == null)
        {
            return NotFound(new { message = "Material item not found." });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        item.ConvertedToRfqId = rfqId;
        item.ConvertedAt = now;
        item.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(
            "material_requisition_item",
            item.Id.ToString(),
            "convert_to_rfq",
            new { rfq_id = rfqId, requisition_id = id },
            user,
            cancellationToken);

        return Ok(new { message = "Material item marked as converted to RFQ." });
    }

    [HttpPost("{id:int}/convert-to-rfq")]
    public async Task<IActionResult> ConvertRequisitionToRfq(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var requisition = await _dbContext.MaterialRequisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition == null)
        {
            return NotFound(new { message = "Requisition not found." });
        }

        if (!string.Equals(requisition.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only approved requisitions can be converted to RFQ." });
        }

        if (requisition.ConvertedToRfqId.HasValue)
        {
            return BadRequest(new { message = "Requisition has already been converted to RFQ." });
        }

        return Ok(new
        {
            data = requisition,
            message = "Requisition ready for RFQ conversion. Use this data to create an RFQ.",
        });
    }
}
