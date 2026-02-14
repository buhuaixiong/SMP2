using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RequisitionsController
{
    [HttpGet("{id:int}/attachments/{attachmentId:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken cancellationToken)
    {
        if (id <= 0 || attachmentId <= 0)
        {
            return BadRequest(new { message = "Invalid ID." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var requisition = await _dbContext.MaterialRequisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (requisition == null)
        {
            return NotFound(new { message = "Requisition not found." });
        }

        var isProcurement = IsProcurementUser(user);
        var isDepartment = IsDepartmentUser(user);

        if (!isProcurement && !isDepartment)
        {
            return StatusCode(403, new { message = "Access denied. Only department users and procurement staff can access requisitions." });
        }

        if (!isProcurement && !string.Equals(requisition.RequestingPersonId, user.Id, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        var attachment = await _dbContext.MaterialRequisitionAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.RequisitionId == id, cancellationToken);

        if (attachment == null)
        {
            await LogAuditAsync(
                "requisition_attachment",
                attachmentId.ToString(),
                "download_failed",
                new { reason = "attachment_not_found", requisition_id = id },
                user,
                cancellationToken);
            return NotFound(new { message = "Attachment not found." });
        }

        var filePath = Path.Combine(ResolveUploadDirectory(), attachment.StoredName);
        if (!System.IO.File.Exists(filePath))
        {
            await LogAuditAsync(
                "requisition_attachment",
                attachmentId.ToString(),
                "download_failed",
                new { reason = "file_not_found_on_disk", requisition_id = id },
                user,
                cancellationToken);
            return NotFound(new { message = "File not found on server." });
        }

        await LogAuditAsync(
            "requisition_attachment",
            attachmentId.ToString(),
            "download",
            new
            {
                requisition_id = id,
                file_name = attachment.OriginalName,
                file_size = attachment.FileSize,
            },
            user,
            cancellationToken);

        var contentType = string.IsNullOrWhiteSpace(attachment.FileType)
            ? "application/octet-stream"
            : attachment.FileType;

        return PhysicalFile(filePath, contentType, attachment.OriginalName);
    }
}
