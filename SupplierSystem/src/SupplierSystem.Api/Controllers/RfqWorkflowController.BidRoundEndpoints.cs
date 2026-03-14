using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpGet("{id:int}/supplier-comparison-history")]
    public async Task<IActionResult> GetSupplierComparisonHistory(int id, CancellationToken cancellationToken)
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

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var rfq = await GetRfqWithLineItemsAsync(id, cancellationToken);
        if (rfq == null)
        {
            return NotFound(new { message = "RFQ not found." });
        }

        var visibility = await _rfqWorkflowStore.GetQuoteVisibilityAsync(id, HttpContext.GetAuthUser()!, cancellationToken);
        var rounds = await _rfqWorkflowStore.LoadBidRoundsAsync(id, cancellationToken);
        var currentRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: true, cancellationToken);
        var latestRoundId = rounds.FirstOrDefault()?.Id;
        var allPriceComparisons = await _rfqWorkflowStore.LoadAllRoundPriceComparisonAttachmentsAsync(id, cancellationToken);

        var roundData = new List<Dictionary<string, object?>>();
        foreach (var round in rounds)
        {
            var summary = await BuildBidRoundSummaryAsync(
                id,
                round,
                visibility.Context.BidRoundId == round.Id ? visibility.Context : null,
                isLatest: latestRoundId == round.Id,
                cancellationToken);

            var invitedSuppliers = await _rfqWorkflowStore.LoadInvitedSupplierRowsAsync(id, round.Id, cancellationToken);
            var quoteRows = await _rfqWorkflowStore.QueryQuoteListRows(id, round.Id)
                .OrderByDescending(row => row.Quote.SubmittedAt)
                .ThenByDescending(row => row.Quote.Id)
                .ToListAsync(cancellationToken);

            var quotes = new List<Dictionary<string, object?>>();
            foreach (var row in quoteRows)
            {
                var quoteDict = NodeCaseMapper.ToSnakeCaseDictionary(row.Quote);
                quoteDict["companyName"] = row.CompanyName;
                quoteDict["supplierName"] = row.CompanyName;
                quoteDict["companyId"] = row.CompanyId;
                quotes.Add(await BuildQuoteResponseAsync(quoteDict, row.Quote.Currency ?? DefaultCurrency, cancellationToken));
            }

            var roundEntry = new Dictionary<string, object?>
            {
                ["summary"] = summary,
                ["invitedSuppliers"] = invitedSuppliers.Select(s => new Dictionary<string, object?>
                {
                    ["id"] = s.Id,
                    ["companyId"] = s.CompanyId,
                    ["supplierCode"] = s.SupplierCode,
                    ["vendorCode"] = s.VendorCode,
                    ["companyName"] = s.CompanyName,
                    ["stage"] = s.Stage,
                    ["invitationStatus"] = s.InvitationStatus,
                    ["quoteStatus"] = NormalizeInvitedSupplierQuoteStatus(s.QuoteStatus),
                    ["quoteSubmittedAt"] = s.QuoteSubmittedAt,
                }).ToList(),
                ["quotes"] = quotes,
                ["priceComparisons"] = allPriceComparisons
                    .Where(item => item.BidRoundId == round.Id)
                    .Select(NodeCaseMapper.ToSnakeCaseDictionary)
                    .ToList(),
                ["externalInvitations"] = (await _rfqWorkflowStore.LoadExternalInvitationsAsync(id, round.Id, cancellationToken))
                    .Select(NodeCaseMapper.ToSnakeCaseDictionary)
                    .ToList(),
            };

            roundData.Add(roundEntry);
        }

        return Ok(new
        {
            data = new Dictionary<string, object?>
            {
                ["rfqId"] = id,
                ["title"] = rfq.TryGetValue("title", out var title) ? title : null,
                ["currentRoundId"] = currentRound?.Id,
                ["latestRoundId"] = latestRoundId,
                ["rounds"] = roundData,
            }
        });
    }

    [HttpGet("{id:int}/bid-rounds")]
    public async Task<IActionResult> GetBidRounds(int id, CancellationToken cancellationToken)
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

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: true, cancellationToken);
        if (rfq == null)
        {
            return NotFound(new { message = "RFQ not found." });
        }

        var visibility = await _rfqWorkflowStore.GetQuoteVisibilityAsync(id, HttpContext.GetAuthUser()!, cancellationToken);
        var rounds = await _rfqWorkflowStore.LoadBidRoundsAsync(id, cancellationToken);
        var currentRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: true, cancellationToken);
        var latestRoundId = rounds.FirstOrDefault()?.Id;

        var data = new List<Dictionary<string, object?>>();
        foreach (var round in rounds)
        {
            var summary = await BuildBidRoundSummaryAsync(
                id,
                round,
                visibility.Context.BidRoundId == round.Id ? visibility.Context : null,
                isLatest: latestRoundId == round.Id,
                cancellationToken);

            if (summary != null)
            {
                summary["isCurrent"] = currentRound?.Id == round.Id;
                data.Add(summary);
            }
        }

        return Ok(new { data });
    }

    [HttpPost("{id:int}/extend-bid-deadline")]
    public async Task<IActionResult> ExtendBidDeadline(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
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

        var newDeadlineValue = ReadStringValue(body, "newDeadline");
        var reason = ReadStringValue(body, "reason");
        if (!TryParseBidRoundDate(newDeadlineValue, out var newDeadlineUtc))
        {
            return BadRequest(new { message = "newDeadline must be a valid date." });
        }

        try
        {
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var currentBidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: false, cancellationToken);
            if (currentBidRound == null)
            {
                return BadRequest(new { message = "RFQ has no current bidding round." });
            }

            if (!string.Equals(currentBidRound.Status, "published", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only a published bidding round can be extended." });
            }

            var visibility = await _rfqWorkflowStore.GetQuoteVisibilityAsync(id, user!, cancellationToken);
            if (IsOpenedBidRound(currentBidRound, visibility.Context))
            {
                return BadRequest(new { message = "The current bidding round has already been opened and can no longer be extended." });
            }

            var pendingCount = Math.Max(visibility.Context.InvitedCount - visibility.Context.SubmittedCount, 0);
            if (pendingCount <= 0)
            {
                return BadRequest(new { message = "Deadline extension is only allowed while invited suppliers are still pending." });
            }

            if (newDeadlineUtc <= DateTime.UtcNow)
            {
                return BadRequest(new { message = "newDeadline must be later than the current time." });
            }

            if (TryParseBidRoundDate(currentBidRound.BidDeadline ?? rfq.ValidUntil, out var currentDeadlineUtc) &&
                newDeadlineUtc <= currentDeadlineUtc)
            {
                return BadRequest(new { message = "newDeadline must be later than the current round deadline." });
            }

            var now = FormatBidRoundDate(DateTime.UtcNow);
            currentBidRound.BidDeadline = FormatBidRoundDate(newDeadlineUtc);
            currentBidRound.ExtensionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            currentBidRound.UpdatedAt = now;
            rfq.ValidUntil = currentBidRound.BidDeadline;
            rfq.UpdatedAt = now;

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            await LogAuditAsync(
                "rfq",
                id.ToString(CultureInfo.InvariantCulture),
                "extend_bid_deadline",
                new
                {
                    bidRoundId = currentBidRound.Id,
                    roundNumber = currentBidRound.RoundNumber,
                    oldDeadline = currentDeadlineUtc == default ? null : FormatBidRoundDate(currentDeadlineUtc),
                    newDeadline = currentBidRound.BidDeadline,
                    reason,
                },
                user!,
                cancellationToken);

            var refreshedContext = new QuoteVisibilityContext
            {
                RfqExists = visibility.Context.RfqExists,
                BidRoundId = currentBidRound.Id,
                RoundNumber = currentBidRound.RoundNumber,
                InvitedCount = visibility.Context.InvitedCount,
                SubmittedCount = visibility.Context.SubmittedCount,
                DeadlinePassed = false,
                AllSubmitted = visibility.Context.AllSubmitted,
                Unlocked = visibility.Context.AllSubmitted,
                Opened = visibility.Context.AllSubmitted,
                Deadline = currentBidRound.BidDeadline,
            };

            var data = await BuildBidRoundSummaryAsync(id, currentBidRound, refreshedContext, true, cancellationToken);
            return Ok(new { message = "Bid deadline extended successfully.", data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend RFQ bid deadline.");
            return StatusCode(500, new { message = "Failed to extend RFQ bid deadline." });
        }
    }

    [HttpPost("{id:int}/start-next-round")]
    public async Task<IActionResult> StartNextBidRound(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
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

        var deadlineValue = ReadStringValue(body, "deadline");
        var reason = ReadStringValue(body, "reason");
        var supplierIds = ReadSupplierIds(body)
            .Where(supplierId => supplierId > 0)
            .Distinct()
            .ToList();

        if (!TryParseBidRoundDate(deadlineValue, out var newDeadlineUtc))
        {
            return BadRequest(new { message = "deadline must be a valid date." });
        }

        if (supplierIds.Count == 0)
        {
            return BadRequest(new { message = "At least one supplierId is required to start the next round." });
        }

        try
        {
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var currentBidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: false, cancellationToken);
            if (currentBidRound == null)
            {
                return BadRequest(new { message = "RFQ has no current bidding round." });
            }

            if (IsClosedBidRound(currentBidRound))
            {
                return BadRequest(new { message = "The current bidding round is already closed." });
            }

            var visibility = await _rfqWorkflowStore.GetQuoteVisibilityAsync(id, user!, cancellationToken);
            if (!IsOpenedBidRound(currentBidRound, visibility.Context))
            {
                return BadRequest(new { message = "The next bidding round can only be started after the current round is opened." });
            }

            if (newDeadlineUtc <= DateTime.UtcNow)
            {
                return BadRequest(new { message = "deadline must be later than the current time." });
            }

            await using var transaction = await _rfqWorkflowStore.BeginTransactionAsync(cancellationToken);

            var now = FormatBidRoundDate(DateTime.UtcNow);
            currentBidRound.OpenedAt ??= now;
            currentBidRound.Status = "closed";
            currentBidRound.ClosedAt = now;
            currentBidRound.UpdatedAt = now;

            var nextBidRound = new RfqBidRound
            {
                RfqId = rfq.Id,
                RoundNumber = currentBidRound.RoundNumber + 1,
                BidDeadline = FormatBidRoundDate(newDeadlineUtc),
                Status = "published",
                CreatedBy = user!.Id,
                CreatedAt = now,
                UpdatedAt = now,
                ExtensionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                StartedFromRoundId = currentBidRound.Id,
            };

            _rfqWorkflowStore.AddBidRound(nextBidRound);
            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            foreach (var supplierId in supplierIds)
            {
                _rfqWorkflowStore.AddSupplierInvitation(new SupplierRfqInvitation
                {
                    RfqId = (int)rfq.Id,
                    BidRoundId = nextBidRound.Id,
                    SupplierId = supplierId,
                    Status = "pending",
                    InvitedAt = now,
                    UpdatedAt = now,
                });
            }

            rfq.Status = "published";
            rfq.ValidUntil = nextBidRound.BidDeadline;
            rfq.SelectedQuoteId = null;
            rfq.ReviewCompletedAt = null;
            rfq.UpdatedAt = now;

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync(
                "rfq",
                id.ToString(CultureInfo.InvariantCulture),
                "start_next_bid_round",
                new
                {
                    previousBidRoundId = currentBidRound.Id,
                    previousRoundNumber = currentBidRound.RoundNumber,
                    bidRoundId = nextBidRound.Id,
                    roundNumber = nextBidRound.RoundNumber,
                    deadline = nextBidRound.BidDeadline,
                    supplierIds,
                    reason,
                },
                user!,
                cancellationToken);

            var data = await BuildBidRoundSummaryAsync(id, nextBidRound, null, true, cancellationToken);
            return StatusCode(201, new { message = "Next bidding round started successfully.", data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start next RFQ bid round.");
            return StatusCode(500, new { message = "Failed to start next RFQ bid round." });
        }
    }
}
