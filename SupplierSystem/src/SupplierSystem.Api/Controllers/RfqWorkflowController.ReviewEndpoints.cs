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
    [HttpPut("{id:int}/view-link")]
    public async Task<IActionResult> UpdateViewLink(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireAnyPermission(user, Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var rawViewLink = ReadStringValue(body, "viewLink")?.Trim();
        if (string.IsNullOrWhiteSpace(rawViewLink))
        {
            return BadRequest(new { message = "viewLink is required." });
        }

        var normalizedViewLink = NormalizeViewLink(rawViewLink);
        if (normalizedViewLink == null)
        {
            return BadRequest(new { message = "viewLink must be a valid HTTP or HTTPS URL." });
        }

        try
        {
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            rfq.ViewLink = normalizedViewLink;
            rfq.UpdatedAt = DateTime.UtcNow.ToString("o");

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "update_view_link",
                    new { viewLink = normalizedViewLink },
                    user,
                    cancellationToken);
            }

            return Ok(new
            {
                message = "View link updated successfully.",
                data = new { viewLink = normalizedViewLink },
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update RFQ view link.");
            return StatusCode(500, new { message = "Failed to update RFQ view link." });
        }
    }

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
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
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

            var quote = await _rfqWorkflowStore.FindQuoteAsync(selectedQuoteId.Value, id, asNoTracking: false, cancellationToken);
            if (quote == null)
            {
                return NotFound(new { message = "Selected quote not found for this RFQ." });
            }

            var latestBidRound = await _rfqWorkflowStore.FindLatestBidRoundAsync(id, asNoTracking: false, cancellationToken);
            if (latestBidRound != null && quote.BidRoundId != latestBidRound.Id)
            {
                return BadRequest(new { message = "Only quotes from the latest bidding round can be selected for final award." });
            }

            var now = DateTime.UtcNow.ToString("o");
            rfq.SelectedQuoteId = selectedQuoteId;
            rfq.ReviewCompletedAt = now;
            rfq.Status = "closed";
            rfq.UpdatedAt = now;

            if (latestBidRound != null)
            {
                latestBidRound.OpenedAt ??= now;
                latestBidRound.Status = "closed";
                latestBidRound.ClosedAt = now;
                latestBidRound.UpdatedAt = now;
            }

            if (!string.Equals(quote.Status, "selected", StringComparison.OrdinalIgnoreCase))
            {
                quote.Status = "selected";
                quote.UpdatedAt = now;
            }

            _rfqWorkflowStore.AddRfqReview(new RfqReview
            {
                RfqId = id,
                SelectedQuoteId = selectedQuoteId,
                ReviewScores = reviewScores,
                Comments = comments,
                ReviewedBy = user?.Name ?? user?.Id ?? "system",
                ReviewedAt = now,
            });

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);
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
            var quote = await _rfqWorkflowStore.FindQuoteAsync(selectedQuoteId.Value, id, asNoTracking: true, cancellationToken);
            if (quote == null)
            {
                return NotFound(new { message = "Quote not found." });
            }

            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var latestBidRound = await _rfqWorkflowStore.FindLatestBidRoundAsync(id, asNoTracking: false, cancellationToken);
            if (latestBidRound != null && quote.BidRoundId != latestBidRound.Id)
            {
                return BadRequest(new { message = "Only quotes from the latest bidding round can be selected." });
            }

            rfq.SelectedQuoteId = selectedQuoteId;
            rfq.ReviewCompletedAt = DateTime.UtcNow.ToString("o");
            rfq.Status = "under_review";

            if (latestBidRound != null)
            {
                latestBidRound.OpenedAt ??= rfq.ReviewCompletedAt;
                latestBidRound.UpdatedAt = rfq.ReviewCompletedAt;
            }

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);
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
            var currentBidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: true, cancellationToken);
            if (currentBidRound == null)
            {
                return BadRequest(new { message = "RFQ has no current bidding round." });
            }

            var storedFiles = await SaveAttachmentsAsync(new[] { screenshot }, id, cancellationToken);
            var stored = storedFiles.First();

            var record = new PriceComparisonAttachment
            {
                RfqId = id,
                BidRoundId = currentBidRound.Id,
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

            _rfqWorkflowStore.AddPriceComparisonAttachment(record);
            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "upload_price_comparison",
                    new { platform, attachmentId = record.Id, bidRoundId = currentBidRound.Id, roundNumber = currentBidRound.RoundNumber },
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
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!rfq.SelectedQuoteId.HasValue)
            {
                return BadRequest(new { message = "Please select a quote first." });
            }

            var selectedQuote = await _rfqWorkflowStore.FindQuoteAsync(rfq.SelectedQuoteId.Value, id, asNoTracking: true, cancellationToken);
            if (selectedQuote == null)
            {
                return BadRequest(new { message = "Selected quote not found. Please select a valid quote." });
            }

            var latestBidRound = await _rfqWorkflowStore.FindLatestBidRoundAsync(id, asNoTracking: true, cancellationToken);
            if (latestBidRound != null && selectedQuote.BidRoundId != latestBidRound.Id)
            {
                return BadRequest(new { message = "Only quotes from the latest bidding round can be submitted for approval." });
            }

            var lineItemExists = await _rfqWorkflowStore.HasAnyRfqLineItemsAsync(id, cancellationToken);
            if (!lineItemExists)
            {
                return BadRequest(new { message = "RFQ has no line items. Cannot submit for approval." });
            }

            var approvalsExist = await _rfqWorkflowStore.HasAnyRfqApprovalsAsync(id, cancellationToken);
            if (approvalsExist)
            {
                return BadRequest(new { message = "Approval workflow already exists for this RFQ." });
            }

            await using var transaction = await _rfqWorkflowStore.BeginTransactionAsync(cancellationToken);

            var now = DateTime.UtcNow.ToString("o");
            _rfqWorkflowStore.AddRfqApproval(new RfqApproval
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

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);
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

    private static string? NormalizeViewLink(string value)
    {
        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = $"https://{candidate}";
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.ToString();
    }
}
