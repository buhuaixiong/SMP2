using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class ReconciliationController
{
    [HttpPost("upload-invoice")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadInvoice(
        [FromForm] int reconciliationId,
        [FromForm] decimal invoiceAmount,
        [FromForm] string invoiceDate,
        [FromForm] string invoiceNumber,
        [FromForm] IFormFile invoiceFile,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: true, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (user?.SupplierId.HasValue == true &&
            !HasAnyPermission(user, StaffPermissions) &&
            !HasAnyPermission(user, Permissions.SupplierReconciliationUpload))
        {
            return Forbidden();
        }

        if (reconciliationId <= 0)
        {
            return BadRequest("reconciliationId is required.");
        }

        if (invoiceAmount <= 0)
        {
            return BadRequest("invoiceAmount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return BadRequest("invoiceNumber is required.");
        }

        if (!TryParseDate(invoiceDate, out var parsedInvoiceDate))
        {
            return BadRequest("invoiceDate is invalid.");
        }

        if (invoiceFile == null || invoiceFile.Length == 0)
        {
            return BadRequest("invoiceFile is required.");
        }

        if (invoiceFile.Length > 10 * 1024 * 1024)
        {
            return BadRequest("invoiceFile exceeds 10MB limit.");
        }

        var reconciliation = await _dbContext.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == reconciliationId, cancellationToken);
        if (reconciliation == null)
        {
            return NotFound("Reconciliation not found.");
        }

        if (user?.SupplierId.HasValue == true &&
            !HasAnyPermission(user, StaffPermissions) &&
            reconciliation.SupplierId != user.SupplierId.Value)
        {
            return Forbidden();
        }

        var duplicateInvoice = await _dbContext.Invoices.AsNoTracking()
            .AnyAsync(i => i.SupplierId == reconciliation.SupplierId && i.InvoiceNumber == invoiceNumber, cancellationToken);
        if (duplicateInvoice)
        {
            return Conflict("Invoice number already exists.");
        }

        var uploadsRoot = UploadPathHelper.GetReconciliationRoot(_environment);
        var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(invoiceFile.FileName)}";
        var filePath = Path.Combine(uploadsRoot, storedName);

        try
        {
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await invoiceFile.CopyToAsync(stream, cancellationToken);
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var nowText = now.ToString("o");
            var creatorId = TryParseUserId(user);

            var invoice = new Invoice
            {
                SupplierId = reconciliation.SupplierId,
                InvoiceNumber = invoiceNumber.Trim(),
                InvoiceDate = parsedInvoiceDate,
                Amount = invoiceAmount,
                Type = "formal_supplier",
                Status = "pending",
                FileName = invoiceFile.FileName,
                StoredFileName = storedName,
                FilePath = filePath,
                FileSize = invoiceFile.Length,
                FileType = invoiceFile.ContentType,
                CreatedBy = creatorId ?? 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _dbContext.InvoiceFiles.Add(new InvoiceFile
            {
                InvoiceId = invoice.Id,
                ReconciliationId = reconciliation.Id,
                SupplierId = reconciliation.SupplierId,
                OriginalName = invoiceFile.FileName,
                StoredName = storedName,
                StoragePath = filePath,
                FileSize = invoiceFile.Length,
                MimeType = invoiceFile.ContentType,
                UploadedBy = user?.Id,
                UploadedByName = user?.Name,
                UploadedAt = nowText
            });

            var receipt = reconciliation.WarehouseReceiptId.HasValue
                ? await _dbContext.WarehouseReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == reconciliation.WarehouseReceiptId.Value, cancellationToken)
                : null;

            var varianceAmount = receipt != null ? invoiceAmount - receipt.TotalAmount : 0m;
            var variancePercentage = receipt != null ? CalculateVariancePercentage(varianceAmount, invoiceAmount) : 0m;
            var (acceptable, warning) = await GetVarianceThresholdsAsync(cancellationToken);
            var newStatus = receipt != null
                ? ResolveMatchStatus(variancePercentage, acceptable, warning)
                : ReconciliationStateMachine.Statuses.Unmatched;

            var oldStatus = reconciliation.Status ?? ReconciliationStateMachine.Statuses.Pending;
            var statusChanged = !string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase);

            reconciliation.TotalInvoiceAmount = invoiceAmount;
            reconciliation.TotalReceiptAmount = receipt?.TotalAmount ?? reconciliation.TotalReceiptAmount;
            reconciliation.VarianceAmount = receipt != null ? varianceAmount : reconciliation.VarianceAmount;
            reconciliation.VariancePercentage = receipt != null ? variancePercentage : reconciliation.VariancePercentage;
            reconciliation.MatchType = "supplier_upload";
            reconciliation.ConfidenceScore = ResolveConfidenceScore(variancePercentage, receipt != null);
            reconciliation.Status = statusChanged ? newStatus : reconciliation.Status ?? newStatus;
            reconciliation.UpdatedAt = nowText;

            if (statusChanged)
            {
                AddStatusHistory(reconciliation.Id, oldStatus, newStatus, user, "Invoice uploaded");
            }

            if (receipt != null && string.Equals(newStatus, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase))
            {
                _dbContext.ReconciliationVarianceAnalyses.Add(new ReconciliationVarianceAnalysis
                {
                    ReconciliationId = reconciliation.Id,
                    VarianceType = "amount_difference",
                    VarianceAmount = Math.Abs(varianceAmount),
                    ExpectedAmount = receipt.TotalAmount,
                    ActualAmount = invoiceAmount,
                    VariancePercentage = variancePercentage,
                    RootCause = "Invoice variance",
                    Notes = "Invoice upload variance detected",
                    CreatedAt = nowText,
                    UpdatedAt = nowText
                });
            }

            var existingMatch = await _dbContext.InvoiceReconciliationMatches
                .FirstOrDefaultAsync(m => m.ReconciliationId == reconciliation.Id, cancellationToken);

            if (existingMatch == null)
            {
                _dbContext.InvoiceReconciliationMatches.Add(new InvoiceReconciliationMatch
                {
                    ReconciliationId = reconciliation.Id,
                    InvoiceId = invoice.Id,
                    WarehouseReceiptId = reconciliation.WarehouseReceiptId,
                    MatchType = "supplier_upload",
                    MatchConfidence = ResolveConfidenceScore(variancePercentage, receipt != null),
                    InvoiceAmount = invoiceAmount,
                    ReceiptAmount = receipt?.TotalAmount,
                    VarianceAmount = receipt != null ? varianceAmount : null,
                    VariancePercentage = receipt != null ? variancePercentage : null,
                    MatchedAt = nowText,
                    MatchedBy = creatorId,
                    Notes = "Supplier uploaded invoice"
                });
            }
            else
            {
                existingMatch.InvoiceId = invoice.Id;
                existingMatch.WarehouseReceiptId = reconciliation.WarehouseReceiptId;
                existingMatch.MatchType = "supplier_upload";
                existingMatch.MatchConfidence = ResolveConfidenceScore(variancePercentage, receipt != null);
                existingMatch.InvoiceAmount = invoiceAmount;
                existingMatch.ReceiptAmount = receipt?.TotalAmount;
                existingMatch.VarianceAmount = receipt != null ? varianceAmount : null;
                existingMatch.VariancePercentage = receipt != null ? variancePercentage : null;
                existingMatch.MatchedAt = nowText;
                existingMatch.MatchedBy = creatorId;
                existingMatch.Notes = "Supplier uploaded invoice";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Success(new
            {
                invoiceId = invoice.Id,
                reconciliationId = reconciliation.Id,
                fileName = invoiceFile.FileName,
                fileSize = invoiceFile.Length
            });
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return InternalError("Failed to upload invoice.");
        }
    }

    [HttpPost("match-invoice")]
    public async Task<IActionResult> MatchInvoice(
        [FromBody] InvoiceMatchRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: false, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (request.ReconciliationId <= 0 || request.InvoiceId <= 0)
        {
            return BadRequest("reconciliationId and invoiceId are required.");
        }

        if (request.MatchedAmount <= 0)
        {
            return BadRequest("matchedAmount must be greater than zero.");
        }

        var reconciliation = await _dbContext.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == request.ReconciliationId, cancellationToken);
        if (reconciliation == null)
        {
            return NotFound("Reconciliation not found.");
        }

        var invoice = await _dbContext.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
        if (invoice == null)
        {
            return NotFound("Invoice not found.");
        }

        if (invoice.SupplierId != reconciliation.SupplierId)
        {
            return BadRequest("Invoice supplier does not match reconciliation supplier.");
        }

        var receipt = reconciliation.WarehouseReceiptId.HasValue
            ? await _dbContext.WarehouseReceipts.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == reconciliation.WarehouseReceiptId.Value, cancellationToken)
            : null;

        var receiptAmount = receipt?.TotalAmount ?? request.MatchedAmount;
        var varianceAmount = request.MatchedAmount - receiptAmount;
        var variancePercentage = CalculateVariancePercentage(varianceAmount, request.MatchedAmount);
        var (acceptable, warning) = await GetVarianceThresholdsAsync(cancellationToken);
        var newStatus = ResolveMatchStatus(variancePercentage, acceptable, warning);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var nowText = DateTime.UtcNow.ToString("o");
        var matchUserId = TryParseUserId(user);

        var oldStatus = reconciliation.Status ?? ReconciliationStateMachine.Statuses.Pending;
        var statusChanged = !string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase);

        reconciliation.TotalInvoiceAmount = request.MatchedAmount;
        reconciliation.TotalReceiptAmount = receiptAmount;
        reconciliation.VarianceAmount = varianceAmount;
        reconciliation.VariancePercentage = variancePercentage;
        reconciliation.MatchType = "manual";
        reconciliation.ConfidenceScore = ResolveConfidenceScore(variancePercentage, true);
        reconciliation.Status = statusChanged ? newStatus : reconciliation.Status ?? newStatus;
        reconciliation.Notes = request.VarianceReason ?? reconciliation.Notes;
        reconciliation.UpdatedAt = nowText;

        if (Math.Abs(invoice.Amount - request.MatchedAmount) > 0.0001m)
        {
            invoice.Amount = request.MatchedAmount;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        if (statusChanged)
        {
            AddStatusHistory(reconciliation.Id, oldStatus, newStatus, user, "Manual match");
        }

        if (string.Equals(newStatus, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase))
        {
            _dbContext.ReconciliationVarianceAnalyses.Add(new ReconciliationVarianceAnalysis
            {
                ReconciliationId = reconciliation.Id,
                VarianceType = "amount_difference",
                VarianceAmount = Math.Abs(varianceAmount),
                ExpectedAmount = receiptAmount,
                ActualAmount = request.MatchedAmount,
                VariancePercentage = variancePercentage,
                RootCause = request.VarianceReason,
                Notes = "Manual match variance",
                CreatedAt = nowText,
                UpdatedAt = nowText
            });
        }

        var existingMatch = await _dbContext.InvoiceReconciliationMatches
            .FirstOrDefaultAsync(m => m.ReconciliationId == reconciliation.Id, cancellationToken);

        if (existingMatch == null)
        {
            _dbContext.InvoiceReconciliationMatches.Add(new InvoiceReconciliationMatch
            {
                ReconciliationId = reconciliation.Id,
                InvoiceId = invoice.Id,
                WarehouseReceiptId = reconciliation.WarehouseReceiptId,
                MatchType = "manual",
                MatchConfidence = ResolveConfidenceScore(variancePercentage, true),
                InvoiceAmount = request.MatchedAmount,
                ReceiptAmount = receiptAmount,
                VarianceAmount = varianceAmount,
                VariancePercentage = variancePercentage,
                MatchedAt = nowText,
                MatchedBy = matchUserId,
                Notes = request.VarianceReason
            });
        }
        else
        {
            existingMatch.InvoiceId = invoice.Id;
            existingMatch.WarehouseReceiptId = reconciliation.WarehouseReceiptId;
            existingMatch.MatchType = "manual";
            existingMatch.MatchConfidence = ResolveConfidenceScore(variancePercentage, true);
            existingMatch.InvoiceAmount = request.MatchedAmount;
            existingMatch.ReceiptAmount = receiptAmount;
            existingMatch.VarianceAmount = varianceAmount;
            existingMatch.VariancePercentage = variancePercentage;
            existingMatch.MatchedAt = nowText;
            existingMatch.MatchedBy = matchUserId;
            existingMatch.Notes = request.VarianceReason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Success(new
        {
            reconciliationId = reconciliation.Id,
            invoiceId = invoice.Id,
            matchedAmount = request.MatchedAmount,
            varianceAmount,
            status = reconciliation.Status
        });
    }

    [HttpPut("confirm")]
    public async Task<IActionResult> ConfirmReconciliation(
        [FromBody] ReconciliationConfirmRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: false, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (request.ReconciliationId <= 0)
        {
            return BadRequest("reconciliationId is required.");
        }

        var reconciliation = await _dbContext.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == request.ReconciliationId, cancellationToken);
        if (reconciliation == null)
        {
            return NotFound("Reconciliation not found.");
        }

        if (!string.IsNullOrWhiteSpace(reconciliation.ConfirmedAt))
        {
            return BadRequest("Reconciliation already confirmed.");
        }

        var nowText = DateTime.UtcNow.ToString("o");

        if (request.Confirm)
        {
            reconciliation.ConfirmedBy = TryParseUserId(user);
            reconciliation.ConfirmedAt = nowText;
            reconciliation.Notes = request.Notes ?? reconciliation.Notes;
            AddStatusHistory(reconciliation.Id, reconciliation.Status, reconciliation.Status ?? ReconciliationStateMachine.Statuses.Pending, user, "Accountant confirmed");
        }
        else
        {
            reconciliation.Notes = request.Notes ?? reconciliation.Notes;
            AddStatusHistory(reconciliation.Id, reconciliation.Status, reconciliation.Status ?? ReconciliationStateMachine.Statuses.Pending, user, "Accountant rejected");
        }

        reconciliation.UpdatedAt = nowText;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Success(new
        {
            reconciliationId = reconciliation.Id,
            confirmed = request.Confirm,
            confirmedAt = request.Confirm ? reconciliation.ConfirmedAt : null
        });
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] ReconciliationStatusUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: false, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest("status is required.");
        }

        var reconciliation = await _dbContext.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reconciliation == null)
        {
            return NotFound("Reconciliation not found.");
        }

        if (string.Equals(request.Status, ReconciliationStateMachine.Statuses.Variance, StringComparison.OrdinalIgnoreCase) &&
            reconciliation.VarianceAmount == null &&
            string.IsNullOrWhiteSpace(request.Notes))
        {
            request.Notes = "Variance flagged";
        }

        var nowText = DateTime.UtcNow.ToString("o");

        try
        {
            await _stateMachine.TransitionAsync(
                reconciliation,
                request.Status,
                user,
                request.Notes,
                async (entity, status, ct) =>
                {
                    entity.Status = status;
                    entity.Notes = request.Notes ?? entity.Notes;
                    entity.UpdatedAt = nowText;
                    await _dbContext.SaveChangesAsync(ct);
                    return entity;
                },
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return Success(new
        {
            reconciliationId = reconciliation.Id,
            status = reconciliation.Status
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteReconciliation(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAccess(user, allowSupplier: false, allowStaff: true);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var reconciliation = await _dbContext.Reconciliations
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (reconciliation == null)
        {
            return NotFound("Reconciliation not found.");
        }

        var matches = await _dbContext.InvoiceReconciliationMatches
            .Where(m => m.ReconciliationId == id)
            .ToListAsync(cancellationToken);
        var analyses = await _dbContext.ReconciliationVarianceAnalyses
            .Where(v => v.ReconciliationId == id)
            .ToListAsync(cancellationToken);
        var histories = await _dbContext.ReconciliationStatusHistories
            .Where(h => h.ReconciliationId == id)
            .ToListAsync(cancellationToken);
        var invoiceFiles = await _dbContext.InvoiceFiles
            .Where(f => f.ReconciliationId == id)
            .ToListAsync(cancellationToken);

        _dbContext.InvoiceReconciliationMatches.RemoveRange(matches);
        _dbContext.ReconciliationVarianceAnalyses.RemoveRange(analyses);
        _dbContext.ReconciliationStatusHistories.RemoveRange(histories);
        _dbContext.InvoiceFiles.RemoveRange(invoiceFiles);
        _dbContext.Reconciliations.Remove(reconciliation);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Success(new { reconciliationId = id });
    }

    private static decimal CalculateVariancePercentage(decimal varianceAmount, decimal invoiceAmount)
    {
        if (invoiceAmount == 0)
        {
            return 0;
        }

        return Math.Abs(varianceAmount) / invoiceAmount * 100;
    }

    private static string ResolveMatchStatus(decimal variancePercentage, decimal acceptable, decimal warning)
    {
        if (variancePercentage <= acceptable)
        {
            return ReconciliationStateMachine.Statuses.Matched;
        }

        if (variancePercentage <= warning)
        {
            return ReconciliationStateMachine.Statuses.Variance;
        }

        return ReconciliationStateMachine.Statuses.Unmatched;
    }

    private static decimal ResolveConfidenceScore(decimal variancePercentage, bool hasReceipt)
    {
        if (!hasReceipt)
        {
            return 0;
        }

        var confidence = 100 - variancePercentage;
        if (confidence < 0)
        {
            return 0;
        }

        return confidence > 100 ? 100 : confidence;
    }

    private static int? TryParseUserId(AuthUser? user)
    {
        if (user == null)
        {
            return null;
        }

        return int.TryParse(user.Id, out var parsed) ? parsed : null;
    }

    private void AddStatusHistory(
        int reconciliationId,
        string? fromStatus,
        string? toStatus,
        AuthUser? user,
        string? reason)
    {
        _dbContext.ReconciliationStatusHistories.Add(new ReconciliationStatusHistory
        {
            ReconciliationId = reconciliationId,
            FromStatus = fromStatus,
            ToStatus = toStatus ?? ReconciliationStateMachine.Statuses.Pending,
            ChangedBy = TryParseUserId(user),
            ChangedAt = DateTime.UtcNow.ToString("o"),
            Reason = reason,
            Notes = reason
        });
    }

    public sealed class InvoiceMatchRequest
    {
        public int InvoiceId { get; set; }
        public int ReconciliationId { get; set; }
        public decimal MatchedAmount { get; set; }
        public decimal? MatchedQuantity { get; set; }
        public string? VarianceReason { get; set; }
    }

    public sealed class ReconciliationConfirmRequest
    {
        public int ReconciliationId { get; set; }
        public bool Confirm { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class ReconciliationStatusUpdateRequest
    {
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }
}
