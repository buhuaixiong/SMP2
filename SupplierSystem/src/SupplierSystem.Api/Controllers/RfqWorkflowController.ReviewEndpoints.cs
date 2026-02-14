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
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> ReviewRfq(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(user,
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var selectedQuoteId = ReadIntValue(body, "selectedQuoteId");
        if (!selectedQuoteId.HasValue)
        {
            return BadRequest(new { message = "selectedQuoteId is required." });
        }

        var comments = ReadStringValue(body, "comments");
        var reviewScores = JsonHelper.TryGetProperty(body, "reviewScores", out var scoreElement)
            ? scoreElement.GetRawText()
            : null;

        if (string.IsNullOrWhiteSpace(reviewScores))
        {
            return BadRequest(new { message = "reviewScores is required." });
        }

        try
        {
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!string.Equals(rfq.Status, "in_progress", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rfq.Status, "published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rfq.Status, "under_review", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "RFQ is not in a state that allows review." });
            }

            var quote = await _dbContext.Quotes.FirstOrDefaultAsync(
                q => q.Id == selectedQuoteId && q.RfqId == id, cancellationToken);
            if (quote == null)
            {
                return NotFound(new { message = "Selected quote not found for this RFQ." });
            }

            var now = DateTime.UtcNow.ToString("o");
            rfq.SelectedQuoteId = selectedQuoteId;
            rfq.ReviewCompletedAt = now;
            rfq.Status = "closed";
            rfq.UpdatedAt = now;

            if (!string.Equals(quote.Status, "selected", StringComparison.OrdinalIgnoreCase))
            {
                quote.Status = "selected";
                quote.UpdatedAt = now;
            }

            _dbContext.RfqReviews.Add(new RfqReview
            {
                RfqId = id,
                SelectedQuoteId = selectedQuoteId,
                ReviewScores = reviewScores,
                Comments = comments,
                ReviewedBy = user?.Name ?? user?.Id ?? "system",
                ReviewedAt = now,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _priceAuditService.SyncSelectedQuoteForRfqAsync(id, selectedQuoteId, cancellationToken);

            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "review",
                    new { selectedQuoteId, comments }, user, cancellationToken);
            }

            return Ok(new { message = "RFQ review completed successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to review RFQ.");
            return StatusCode(500, new { message = "Failed to review RFQ." });
        }
    }
    [HttpPost("{id:int}/select-quote")]
    public async Task<IActionResult> SelectQuote(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
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

        var selectedQuoteId = ReadIntValue(body, "selectedQuoteId");
        var selectionReason = ReadStringValue(body, "selectionReason");
        if (!selectedQuoteId.HasValue)
        {
            return BadRequest(new { message = "selectedQuoteId is required." });
        }

        try
        {
            var quote = await _dbContext.Quotes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == selectedQuoteId && q.RfqId == id, cancellationToken);
            if (quote == null)
            {
                return NotFound(new { message = "Quote not found." });
            }

            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            rfq.SelectedQuoteId = selectedQuoteId;
            rfq.ReviewCompletedAt = DateTime.UtcNow.ToString("o");
            rfq.Status = "under_review";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _priceAuditService.SyncSelectedQuoteForRfqAsync(id, selectedQuoteId, cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "select_quote",
                    new { selectedQuoteId, selectionReason },
                    user,
                    cancellationToken);
            }

            return Ok(new { message = "Quote selected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select quote.");
            return StatusCode(500, new { message = "Failed to select quote." });
        }
    }

    [HttpPost("{id:int}/price-comparison")]
    public async Task<IActionResult> UploadPriceComparison(int id, CancellationToken cancellationToken)
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

        if (!Request.HasFormContentType)
        {
            return BadRequest(new { message = "Screenshot file is required." });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var platform = form.TryGetValue("platform", out var platformValue) ? platformValue.ToString() : null;
        var productUrl = form.TryGetValue("productUrl", out var productUrlValue) ? productUrlValue.ToString() : null;
        var platformPrice = form.TryGetValue("platformPrice", out var priceValue) ? priceValue.ToString() : null;
        var notes = form.TryGetValue("notes", out var notesValue) ? notesValue.ToString() : null;
        var lineItemId = form.TryGetValue("lineItemId", out var lineItemValue) ? lineItemValue.ToString() : null;
        var screenshot = form.Files.GetFile("screenshot") ?? form.Files.FirstOrDefault();

        var validPlatforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1688", "zkh", "jd" };
        if (string.IsNullOrWhiteSpace(platform) || !validPlatforms.Contains(platform))
        {
            return BadRequest(new { message = "Invalid platform. Must be one of: 1688, zkh, jd" });
        }

        if (screenshot == null)
        {
            return BadRequest(new { message = "Screenshot file is required." });
        }

        try
        {
            var storedFiles = await SaveAttachmentsAsync(new[] { screenshot }, id, cancellationToken);
            var stored = storedFiles.First();

            var record = new PriceComparisonAttachment
            {
                RfqId = id,
                LineItemId = ReadIntValue(lineItemId),
                Platform = platform,
                FileName = stored.OriginalName,
                FilePath = stored.FilePath,
                OriginalFileName = stored.OriginalName,
                StoredFileName = stored.StoredName,
                ProductUrl = string.IsNullOrWhiteSpace(productUrl) ? null : productUrl,
                PlatformPrice = ReadDecimal(platformPrice),
                UploadedBy = HttpContext.GetAuthUser()!.Id,
                UploadedAt = DateTime.UtcNow.ToString("o"),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            };

            _dbContext.PriceComparisonAttachments.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "upload_price_comparison",
                    new { platform, attachmentId = record.Id },
                    user,
                    cancellationToken);
            }

            return StatusCode(201, new
            {
                message = "Price comparison uploaded successfully",
                data = new
                {
                    id = record.Id,
                    platform = record.Platform,
                    fileName = record.FileName,
                    originalFileName = record.OriginalFileName,
                    storedFileName = record.StoredFileName,
                    filePath = record.FilePath,
                },
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload price comparison.");
            return StatusCode(500, new { message = "Failed to upload price comparison." });
        }
    }

    [HttpPost("{id:int}/submit-review")]
    public async Task<IActionResult> SubmitReview(int id, CancellationToken cancellationToken)
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

        try
        {
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!rfq.SelectedQuoteId.HasValue)
            {
                return BadRequest(new { message = "Please select a quote first." });
            }

            var selectedQuote = await _dbContext.Quotes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == rfq.SelectedQuoteId.Value, cancellationToken);
            if (selectedQuote == null)
            {
                return BadRequest(new { message = "Selected quote not found. Please select a valid quote." });
            }

            var lineItemExists = await _dbContext.RfqLineItems.AsNoTracking()
                .AnyAsync(li => li.RfqId == id, cancellationToken);
            if (!lineItemExists)
            {
                return BadRequest(new { message = "RFQ has no line items. Cannot submit for approval." });
            }

            var approvalsExist = await _dbContext.RfqApprovals.AsNoTracking()
                .AnyAsync(a => a.RfqId == id, cancellationToken);
            if (approvalsExist)
            {
                return BadRequest(new { message = "Approval workflow already exists for this RFQ." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow.ToString("o");
            _dbContext.RfqApprovals.Add(new RfqApproval
            {
                RfqId = id,
                StepOrder = 1,
                StepName = "Procurement Director Approval",
                ApproverRole = "procurement_director",
                Status = "pending",
                CreatedAt = now,
            });

            rfq.ApprovalStatus = "pending_director";
            rfq.Status = "pending_director_approval";

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "submit_review",
                    new { approvalStatus = "pending_director" },
                    user,
                    cancellationToken);
            }

            await TryNotifyRfqSubmittedForApprovalAsync(rfq, cancellationToken);

            return Ok(new { message = "Review submitted successfully. Approval workflow initiated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit review.");
            return StatusCode(500, new { message = "Failed to submit review." });
        }
    }
}
