using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Models.Requisitions;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RequisitionsController
{
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> CreateRequisition(
        [FromForm] RequisitionCreateForm form,
        CancellationToken cancellationToken)
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

        var requestingDepartment = form.RequestingDepartment?.Trim();
        var requiredDate = form.RequiredDate?.Trim();

        if (string.IsNullOrWhiteSpace(requestingDepartment) || string.IsNullOrWhiteSpace(requiredDate))
        {
            return BadRequest(new { message = "Requesting department and required date are required." });
        }

        List<RequisitionItemInput> items;
        try
        {
            items = RequisitionPayloadParser.ParseItems(form.Items);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (items.Count == 0)
        {
            return BadRequest(new { message = "At least one material item is required." });
        }

        try
        {
            ValidateItemRequirements(items);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (form.Attachments.Count > 10)
        {
            return BadRequest(new { message = "Attachment limit exceeded. Max 10 files." });
        }

        foreach (var file in form.Attachments)
        {
            if (!IsAllowedAttachment(file))
            {
                return BadRequest(new { message = "Invalid file type. Only PDF, Office documents, and images are allowed." });
            }
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var requisitionNumber = string.IsNullOrWhiteSpace(form.PrNumber)
            ? GenerateRequisitionNumber()
            : form.PrNumber!.Trim();

        var requisition = new MaterialRequisition
        {
            RequisitionNumber = requisitionNumber,
            RequestingDepartment = requestingDepartment,
            RequestingPersonId = user.Id,
            RequestingPersonName = user.Name,
            RequiredDate = requiredDate,
            Priority = string.IsNullOrWhiteSpace(form.Priority) ? "normal" : form.Priority!.Trim(),
            Status = "draft",
            Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            ItemName = "[Multiple Items]",
            Quantity = 0m,
            Unit = "-",
        };

        var uploadDir = ResolveUploadDirectory();
        var storedFiles = new List<string>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.MaterialRequisitions.Add(requisition);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in items)
            {
                _dbContext.MaterialRequisitionItems.Add(new MaterialRequisitionItem
                {
                    RequisitionId = requisition.Id,
                    ItemType = item.ItemType,
                    ItemSubtype = item.ItemSubtype,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    ItemDescription = item.ItemDescription,
                    Specifications = item.Specifications,
                    EstimatedBudget = item.EstimatedBudget,
                    Currency = string.IsNullOrWhiteSpace(item.Currency) ? "CNY" : item.Currency,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            foreach (var file in form.Attachments)
            {
                var extension = Path.GetExtension(file.FileName) ?? string.Empty;
                var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{extension}";
                var path = Path.Combine(uploadDir, storedName);

                await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                storedFiles.Add(path);

                _dbContext.MaterialRequisitionAttachments.Add(new MaterialRequisitionAttachment
                {
                    RequisitionId = requisition.Id,
                    OriginalName = DecodeFileName(file.FileName),
                    StoredName = storedName,
                    FileType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType,
                    FileSize = file.Length,
                    UploadedAt = now,
                    UploadedBy = user.Name,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            foreach (var stored in storedFiles)
            {
                try
                {
                    if (System.IO.File.Exists(stored))
                    {
                        System.IO.File.Delete(stored);
                    }
                }
                catch
                {
                    // ignore cleanup failures
                }
            }

            _logger.LogError(ex, "[Requisitions] Failed to create requisition.");
            return StatusCode(500, new { message = "Failed to create material requisition." });
        }

        await LogAuditAsync(
            "material_requisition",
            requisition.Id.ToString(),
            "create",
            new { requisition_number = requisitionNumber, requesting_department = requestingDepartment, items_count = items.Count },
            user,
            cancellationToken);

        var created = await LoadRequisitionDetailsAsync(requisition.Id, cancellationToken);
        return StatusCode(201, new { data = created });
    }

    [HttpGet]
    public async Task<IActionResult> ListRequisitions(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var page = ParseInt(Request.Query["page"], 1);
        var limit = ParseInt(Request.Query["limit"], 20);
        page = Math.Max(1, page);
        limit = Math.Min(100, Math.Max(1, limit));
        var offset = (page - 1) * limit;

        var status = string.IsNullOrWhiteSpace(Request.Query["status"]) ? null : Request.Query["status"].ToString();
        var requestingDepartment = string.IsNullOrWhiteSpace(Request.Query["requesting_department"])
            ? null
            : Request.Query["requesting_department"].ToString();

        var isProcurement = IsProcurementUser(user);
        var isDepartment = IsDepartmentUser(user);

        if (!isProcurement && !isDepartment)
        {
            return StatusCode(403, new { message = "Access denied. Only department users and procurement staff can access requisitions." });
        }

        if (isDepartment)
        {
            var repaired = await EnsureDepartmentAsync(user, cancellationToken);
            if (repaired == null || string.IsNullOrWhiteSpace(repaired.Department))
            {
                return BadRequest(new
                {
                    message = "Department user must have department field. Please contact administrator.",
                    code = "MISSING_DEPARTMENT_FIELD",
                });
            }
        }

        var query = _dbContext.MaterialRequisitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(requestingDepartment))
        {
            query = query.Where(r => r.RequestingDepartment == requestingDepartment);
        }

        if (isDepartment && !string.IsNullOrWhiteSpace(user.Department))
        {
            query = query.Where(r => r.RequestingDepartment == user.Department);
        }

        var total = await query.CountAsync(cancellationToken);
        var requisitions = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var payload = await BuildRequisitionPayloadAsync(requisitions, cancellationToken);

        return Ok(new
        {
            data = payload,
            pagination = new
            {
                page,
                limit,
                total,
                totalPages = (int)Math.Ceiling(total / (double)limit),
            }
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRequisition(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid requisition ID." });
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

        var details = await LoadRequisitionDetailsAsync(id, cancellationToken);
        return Ok(new { data = details });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRequisition(
        int id,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid requisition ID." });
        }

        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.DepartmentRequisitionManage);
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
            return StatusCode(403, new { message = "Only the requester can edit this requisition." });
        }

        if (!string.Equals(requisition.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only draft requisitions can be edited." });
        }

        var requestingDepartment = RequisitionPayloadParser.GetString(body, "requesting_department", "requestingDepartment");
        var requiredDate = RequisitionPayloadParser.GetString(body, "required_date", "requiredDate");
        var priority = RequisitionPayloadParser.GetString(body, "priority") ?? "normal";
        var notes = RequisitionPayloadParser.GetString(body, "notes");

        if (string.IsNullOrWhiteSpace(requestingDepartment) || string.IsNullOrWhiteSpace(requiredDate))
        {
            return BadRequest(new { message = "Requesting department and required date are required." });
        }

        List<RequisitionItemInput> items;
        try
        {
            items = RequisitionPayloadParser.ParseItems(body.GetProperty("items"));
        }
        catch
        {
            return BadRequest(new { message = "Invalid items format." });
        }

        if (items.Count == 0)
        {
            return BadRequest(new { message = "At least one material item is required." });
        }

        try
        {
            ValidateItemRequirements(items);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            requisition.RequestingDepartment = requestingDepartment;
            requisition.RequiredDate = requiredDate;
            requisition.Priority = priority;
            requisition.Notes = notes;
            requisition.UpdatedAt = now;

            _dbContext.MaterialRequisitions.Update(requisition);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var existingItems = _dbContext.MaterialRequisitionItems.Where(i => i.RequisitionId == id);
            _dbContext.MaterialRequisitionItems.RemoveRange(existingItems);
            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var item in items)
            {
                _dbContext.MaterialRequisitionItems.Add(new MaterialRequisitionItem
                {
                    RequisitionId = id,
                    ItemType = item.ItemType,
                    ItemSubtype = item.ItemSubtype,
                    ItemName = item.ItemName,
                    Quantity = item.Quantity,
                    Unit = item.Unit,
                    ItemDescription = item.ItemDescription,
                    Specifications = item.Specifications,
                    EstimatedBudget = item.EstimatedBudget,
                    Currency = string.IsNullOrWhiteSpace(item.Currency) ? "CNY" : item.Currency,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "[Requisitions] Failed to update requisition.");
            return StatusCode(500, new { message = "Failed to update requisition." });
        }

        await LogAuditAsync(
            "material_requisition",
            id.ToString(),
            "update",
            new { requesting_department = requestingDepartment, items_count = items.Count },
            user,
            cancellationToken);

        var updated = await LoadRequisitionDetailsAsync(id, cancellationToken);
        return Ok(new { data = updated });
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private async Task<object?> LoadRequisitionDetailsAsync(int requisitionId, CancellationToken cancellationToken)
    {
        var requisition = await _dbContext.MaterialRequisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requisitionId, cancellationToken);

        if (requisition == null)
        {
            return null;
        }

        var items = await _dbContext.MaterialRequisitionItems
            .AsNoTracking()
            .Where(i => i.RequisitionId == requisitionId)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var attachments = await _dbContext.MaterialRequisitionAttachments
            .AsNoTracking()
            .Where(a => a.RequisitionId == requisitionId)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

        return new
        {
            requisition.Id,
            requisition.RequisitionNumber,
            requisition.RequestingDepartment,
            requisition.RequestingPersonId,
            requisition.RequestingPersonName,
            requisition.RequiredDate,
            requisition.ItemName,
            requisition.Quantity,
            requisition.Unit,
            requisition.ItemDescription,
            requisition.Specifications,
            requisition.EstimatedBudget,
            requisition.Currency,
            requisition.Priority,
            requisition.AttachmentFiles,
            requisition.Status,
            requisition.SubmittedAt,
            requisition.ApprovedById,
            requisition.ApprovedByName,
            requisition.ApprovedAt,
            requisition.RejectedById,
            requisition.RejectedByName,
            requisition.RejectedAt,
            requisition.RejectionReason,
            requisition.ConvertedToRfqId,
            requisition.CreatedAt,
            requisition.UpdatedAt,
            requisition.Notes,
            items,
            attachments,
        };
    }

    private async Task<IReadOnlyList<object>> BuildRequisitionPayloadAsync(
        IReadOnlyList<MaterialRequisition> requisitions,
        CancellationToken cancellationToken)
    {
        if (requisitions.Count == 0)
        {
            return Array.Empty<object>();
        }

        var requisitionIds = requisitions.Select(r => r.Id).ToList();

        var items = await _dbContext.MaterialRequisitionItems
            .AsNoTracking()
            .Where(i => requisitionIds.Contains(i.RequisitionId))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var attachments = await _dbContext.MaterialRequisitionAttachments
            .AsNoTracking()
            .Where(a => requisitionIds.Contains(a.RequisitionId))
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken);

        var itemsLookup = items.GroupBy(i => i.RequisitionId).ToDictionary(g => g.Key, g => g.ToList());
        var attachmentLookup = attachments.GroupBy(a => a.RequisitionId).ToDictionary(g => g.Key, g => g.ToList());

        var payload = new List<object>(requisitions.Count);
        foreach (var req in requisitions)
        {
            itemsLookup.TryGetValue(req.Id, out var reqItems);
            attachmentLookup.TryGetValue(req.Id, out var reqAttachments);

            payload.Add(new
            {
                req.Id,
                req.RequisitionNumber,
                req.RequestingDepartment,
                req.RequestingPersonId,
                req.RequestingPersonName,
                req.RequiredDate,
                req.ItemName,
                req.Quantity,
                req.Unit,
                req.ItemDescription,
                req.Specifications,
                req.EstimatedBudget,
                req.Currency,
                req.Priority,
                req.AttachmentFiles,
                req.Status,
                req.SubmittedAt,
                req.ApprovedById,
                req.ApprovedByName,
                req.ApprovedAt,
                req.RejectedById,
                req.RejectedByName,
                req.RejectedAt,
                req.RejectionReason,
                req.ConvertedToRfqId,
                req.CreatedAt,
                req.UpdatedAt,
                req.Notes,
                items = reqItems ?? new List<MaterialRequisitionItem>(),
                attachments = reqAttachments ?? new List<MaterialRequisitionAttachment>(),
            });
        }

        return payload;
    }
}
