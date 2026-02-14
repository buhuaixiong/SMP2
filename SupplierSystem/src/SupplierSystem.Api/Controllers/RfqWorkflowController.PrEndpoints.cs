using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpPost("{id:int}/fill-pr")]
    public async Task<IActionResult> FillPr(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var prNumber = ReadStringValue(body, "prNumber");
        var prDate = ReadStringValue(body, "prDate");

        if (string.IsNullOrWhiteSpace(prNumber))
        {
            return BadRequest(new { message = "prNumber is required." });
        }

        if (string.IsNullOrWhiteSpace(prNumber.Trim()))
        {
            return BadRequest(new { message = "PR number must be a non-empty string." });
        }


        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return StatusCode(401, new { message = "Authentication required." });
        }

        try
        {
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!string.Equals(rfq.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "RFQ must be fully approved before filling PR." });
            }


            var existingPr = await _dbContext.RfqPrRecords.AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.RfqId == id, cancellationToken);
            if (existingPr != null)
            {
                return BadRequest(new
                {
                    message = "PR record already exists for this RFQ. Please update or delete the existing record first."
                });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow.ToString("o");

            var record = new RfqPrRecord
            {
                RfqId = id,
                PrNumber = prNumber.Trim(),
                PrDate = string.IsNullOrWhiteSpace(prDate)
                    ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                    : prDate,
                FilledBy = user.Id,
                FilledAt = now,
                DepartmentConfirmerId = null,
                DepartmentConfirmerName = null,
                ConfirmationStatus = "confirmed",
                ConfirmedAt = now,
            };

            _dbContext.RfqPrRecords.Add(record);
            rfq.PrStatus = "confirmed";
            rfq.Status = "completed";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "fill_pr",
                new { prNumber = prNumber.Trim() },
                user,
                cancellationToken);

            return Ok(new { message = "PR filled successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill PR.");
            return StatusCode(500, new { message = "Failed to fill PR." });
        }
    }



    [HttpPost("{id:int}/confirm-pr")]
    public async Task<IActionResult> ConfirmPr(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!string.Equals(user.Role, "department_user", StringComparison.OrdinalIgnoreCase))
        {
            var permissionResult = RequireAnyPermission(user,
                Permissions.DepartmentRequisitionManage,
                Permissions.PurchaserRfqTarget);
            if (permissionResult != null)
            {
                return permissionResult;
            }
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var confirmationStatus = ReadStringValue(body, "confirmationStatus");
        if (string.IsNullOrWhiteSpace(confirmationStatus))
        {
            return BadRequest(new { message = "confirmationStatus is required." });
        }

        var normalizedStatus = confirmationStatus.Trim().ToLowerInvariant();
        if (normalizedStatus != "confirmed" && normalizedStatus != "rejected")
        {
            return BadRequest(new { message = "confirmationStatus must be confirmed or rejected." });
        }

        var confirmationNotes = ReadStringValue(body, "confirmationNotes");
        confirmationNotes = string.IsNullOrWhiteSpace(confirmationNotes) ? null : confirmationNotes.Trim();

        try
        {
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var prRecord = await _dbContext.RfqPrRecords.FirstOrDefaultAsync(pr => pr.RfqId == id, cancellationToken);
            if (prRecord == null)
            {
                return NotFound(new { message = "PR record not found for this RFQ." });
            }

            var now = DateTime.UtcNow.ToString("o");

            prRecord.DepartmentConfirmerId = user.Id;
            prRecord.DepartmentConfirmerName = user.Name;
            prRecord.ConfirmationStatus = normalizedStatus;
            prRecord.ConfirmationNotes = confirmationNotes;
            prRecord.ConfirmedAt = now;

            rfq.PrStatus = normalizedStatus;
            if (string.Equals(normalizedStatus, "confirmed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rfq.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                rfq.Status = "completed";
            }

            rfq.UpdatedAt = now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "confirm_pr",
                new { confirmationStatus = normalizedStatus }, user, cancellationToken);

            var message = normalizedStatus == "confirmed"
                ? "PR confirmed successfully."
                : "PR rejected successfully.";

            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm PR.");
            return StatusCode(500, new { message = "Failed to confirm PR." });
        }
    }

}
