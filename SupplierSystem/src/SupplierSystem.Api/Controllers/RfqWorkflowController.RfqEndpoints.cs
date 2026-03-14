using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.Services.Rfq;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpGet("categories")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetRfqCategories()
    {
        var categories = new Dictionary<string, object?>
        {
            ["equipment"] = new Dictionary<string, object?>
            {
                ["label"] = "Equipment",
                ["subcategories"] = new Dictionary<string, string>
                {
                    ["standard"] = "Standard Equipment",
                    ["non_standard"] = "Non-Standard Equipment",
                },
            },
            ["auxiliary_materials"] = new Dictionary<string, object?>
            {
                ["label"] = "Auxiliary Materials",
                ["subcategories"] = new Dictionary<string, string>
                {
                    ["labor_protection"] = "Labor Protection Supplies",
                    ["office_supplies"] = "Office Supplies",
                    ["production_supplies"] = "Production Supplies",
                    ["accessories"] = "Accessories",
                    ["others"] = "Others",
                },
            },
        };

        return Ok(new { data = categories });
    }

    [HttpPost("import-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public IActionResult ImportFromExcel([FromForm] ImportExcelRequest request)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(new { error = "Please upload an Excel file." });
        }

        var sheetName = string.IsNullOrWhiteSpace(request.SheetName) ? "PRBuyer" : request.SheetName.Trim();
        var headerRow = request.HeaderRow ?? 15;

        try
        {
            using var stream = request.File.OpenReadStream();
            var result = _rfqExcelImportService.Parse(stream, sheetName, headerRow);
            return Ok(new
            {
                data = result,
                message = $"成功导入 {result.Requirements.Count} 个物料行",
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            var details = _environment.IsDevelopment() ? ex.Message : null;
            return StatusCode(500, new { message = "Failed to import Excel file.", details });
        }
    }

[HttpGet("{id}")]
    public async Task<IActionResult> GetRfq(string id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove,
            Permissions.DepartmentRequisitionManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!int.TryParse(id, out var rfqId))
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await GetRfqWithLineItemsAsync(rfqId, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var currentBidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(rfqId, asNoTracking: true, cancellationToken);
            var bidRounds = await _rfqWorkflowStore.LoadBidRoundsAsync(rfqId, cancellationToken);
            var latestBidRoundId = bidRounds.FirstOrDefault()?.Id;

            var invitedSuppliers = await _rfqWorkflowStore.LoadInvitedSupplierRowsAsync(rfqId, currentBidRound?.Id, cancellationToken);

            var invitedSupplierList = invitedSuppliers.Select(s => new Dictionary<string, object?>
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
            }).ToList();

            var externalInvitations = await _rfqWorkflowStore.LoadExternalInvitationsAsync(rfqId, currentBidRound?.Id, cancellationToken);

            var externalInvitationList = externalInvitations
                .Select(NodeCaseMapper.ToSnakeCaseDictionary)
                .ToList();

            var quoteRows = await _rfqWorkflowStore.QueryQuoteListRows(rfqId, currentBidRound?.Id)
                .OrderByDescending(row => row.Quote.SubmittedAt)
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

            var visibility = await _rfqWorkflowStore.GetQuoteVisibilityAsync(rfqId, user!, cancellationToken);
            if (visibility.Locked)
            {
                quotes = new List<Dictionary<string, object?>>();
            }

            var roundSummaries = new List<Dictionary<string, object?>>();
            foreach (var bidRound in bidRounds)
            {
                var summary = await BuildBidRoundSummaryAsync(
                    rfqId,
                    bidRound,
                    visibility.Context.BidRoundId == bidRound.Id ? visibility.Context : null,
                    isLatest: latestBidRoundId == bidRound.Id,
                    cancellationToken);

                if (summary != null)
                {
                    roundSummaries.Add(summary);
                }
            }

            var currentRoundSummary = await BuildBidRoundSummaryAsync(
                rfqId,
                currentBidRound,
                visibility.Context,
                isLatest: latestBidRoundId == currentBidRound?.Id,
                cancellationToken);

            var priceComparisons = await _rfqWorkflowStore.LoadPriceComparisonAttachmentsAsync(rfqId, currentBidRound?.Id, cancellationToken);

            var priceComparisonList = priceComparisons.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();

            var approvals = await _rfqWorkflowStore.LoadApprovalsForRfqAsync(rfqId, cancellationToken);

            var approvalList = approvals.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();

            var prRecord = await _rfqWorkflowStore.FindRfqPrRecordAsync(rfqId, asNoTracking: true, cancellationToken);

            if (!visibility.Locked && string.Equals(user?.Role, "department_user", StringComparison.OrdinalIgnoreCase))
            {
                if (rfq.TryGetValue("lineItems", out var lineItemsObj) &&
                    lineItemsObj is List<object?> lineItemsList)
                {
                    var selectedQuoteIds = new HashSet<int>();
                    foreach (var entry in lineItemsList)
                    {
                        if (entry is Dictionary<string, object?> lineItem &&
                            lineItem.TryGetValue("selectedQuoteId", out var selectedObj) &&
                            selectedObj is int selectedId)
                        {
                            selectedQuoteIds.Add(selectedId);
                        }
                    }

                    if (selectedQuoteIds.Count > 0)
                    {
                        quotes = quotes.Where(q =>
                            q.TryGetValue("id", out var idObj) &&
                            idObj is int quoteId &&
                            selectedQuoteIds.Contains(quoteId)).ToList();
                    }
                    else
                    {
                        quotes = new List<Dictionary<string, object?>>();
                    }
                }
                else if (rfq.TryGetValue("selectedQuoteId", out var selectedQuoteObj) &&
                         selectedQuoteObj is int selectedQuoteId)
                {
                    quotes = quotes.Where(q =>
                        q.TryGetValue("id", out var idObj) &&
                        idObj is int quoteId &&
                        quoteId == selectedQuoteId).ToList();
                }
                else
                {
                    quotes = new List<Dictionary<string, object?>>();
                }
            }

            var data = new Dictionary<string, object?>(rfq)
            {
                ["invitedSuppliers"] = invitedSupplierList,
                ["externalInvitations"] = externalInvitationList,
                ["quotes"] = quotes,
                ["priceComparisons"] = priceComparisonList,
                ["approvals"] = approvalList,
                ["prRecord"] = prRecord == null ? null : NodeCaseMapper.ToSnakeCaseDictionary(prRecord),
                ["quotesVisible"] = !visibility.Locked,
                ["currentRound"] = currentRoundSummary,
                ["rounds"] = roundSummaries,
                ["visibilityReason"] = visibility.Locked
                    ? new
                    {
                        totalInvited = visibility.Context.InvitedCount,
                        submittedCount = visibility.Context.SubmittedCount,
                        deadline = visibility.Context.Deadline,
                        message = QuoteVisibility.QuoteVisibilityLockMessage,
                    }
                    : null,
            };

            return Ok(new { data });
        }
        catch (Exception ex)
        {
            var user = HttpContext.GetAuthUser();
            Console.Error.WriteLine($"[RFQ_GET_ERROR] rfqId={rfqId}; userId={user?.Id}; role={user?.Role}; supplierId={user?.SupplierId}; exception={ex}");
            _logger.LogError(
                ex,
                "Failed to fetch RFQ {RfqId} for user {UserId} with role {Role} and supplier {SupplierId}.",
                rfqId,
                user?.Id,
                user?.Role,
                user?.SupplierId);
            return StatusCode(500, new { message = "Failed to fetch RFQ." });
        }
    }

    [HttpGet("supplier/{id:int}")]
    public async Task<IActionResult> GetSupplierRfq(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var user = HttpContext.GetAuthUser();
        if (user?.SupplierId == null)
        {
            return StatusCode(403, new { message = "Only suppliers can access this RFQ." });
        }

        try
        {
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: true, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var currentBidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: true, cancellationToken);

            var invitation = await _rfqWorkflowStore.FindInvitationAsync(id, user.SupplierId.Value, currentBidRound?.Id, cancellationToken);
            if (invitation == null)
            {
                return StatusCode(403, new { message = "You are not invited to this RFQ." });
            }

            var rfqData = await GetRfqWithLineItemsAsync(id, cancellationToken);
            if (rfqData == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var quoteRows = await _rfqWorkflowStore.LoadQuotesForSupplierAsync(id, user.SupplierId.Value, currentBidRound?.Id, cancellationToken);

            var quotes = new List<Dictionary<string, object?>>();
            foreach (var quote in quoteRows)
            {
                quotes.Add(await BuildQuoteResponseAsync(quote, quote.Currency ?? DefaultCurrency, cancellationToken));
            }

            int? daysRemaining = null;
            var bidDeadline = currentBidRound?.BidDeadline ?? rfq.ValidUntil;
            if (!string.IsNullOrWhiteSpace(bidDeadline) &&
                DateTime.TryParse(bidDeadline, out var deadline))
            {
                var diffTime = deadline - DateTime.UtcNow;
                daysRemaining = (int)Math.Ceiling(diffTime.TotalDays);
            }

            var quoteStatus = quotes.Count > 0 &&
                              quotes[0].TryGetValue("status", out var statusObj)
                ? statusObj?.ToString()
                : null;

            var needsResponse = string.Equals(rfq.Status, "published", StringComparison.OrdinalIgnoreCase) &&
                                (string.IsNullOrWhiteSpace(quoteStatus) ||
                                 string.Equals(quoteStatus, "draft", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(quoteStatus, "withdrawn", StringComparison.OrdinalIgnoreCase));

            var invitationDict = NodeCaseMapper.ToCamelCaseDictionary(invitation);
            invitationDict["rfqStatus"] = rfq.Status;
            invitationDict["quoteStatus"] = quoteStatus ?? "not_submitted";
            invitationDict["validUntil"] = bidDeadline;
            invitationDict["daysRemaining"] = daysRemaining;
            invitationDict["needsResponse"] = needsResponse;
            invitationDict["bidRoundId"] = currentBidRound?.Id;
            invitationDict["roundNumber"] = currentBidRound?.RoundNumber;

            rfqData["quotes"] = quotes;
            rfqData["supplierInvitation"] = invitationDict;
            rfqData["invitation"] = invitationDict;
            rfqData["currentRound"] = await BuildBidRoundSummaryAsync(id, currentBidRound, null, true, cancellationToken);

            return Ok(new { data = rfqData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch supplier RFQ.");
            return StatusCode(500, new { message = "Failed to fetch RFQ." });
        }
    }

    [HttpGet("supplier/invitations")]
    public async Task<IActionResult> GetSupplierInvitations(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user?.SupplierId == null)
        {
            return StatusCode(403, new { message = "Only suppliers can access invitations." });
        }

        var status = string.IsNullOrWhiteSpace(Request.Query["status"]) ? null : Request.Query["status"].ToString();
        var needsResponse = string.Equals(Request.Query["needsResponse"], "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var supplierId = user.SupplierId.Value;

            var query = _rfqWorkflowStore.QuerySupplierInvitationRows(supplierId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(row => row.Invitation.Status == status);
            }

            if (needsResponse)
            {
                query = query.Where(row =>
                    (row.Rfq.Status == "published" || row.Rfq.Status == "in_progress") &&
                    (row.QuoteStatus == null || row.QuoteStatus == "draft" || row.QuoteStatus == "withdrawn"));
            }

            var rows = await query
                .OrderByDescending(row => row.Invitation.InvitedAt)
                .ToListAsync(cancellationToken);

            var results = new List<Dictionary<string, object?>>();
            foreach (var row in rows)
            {
                int? daysRemaining = null;
                var bidDeadline = row.BidRound?.BidDeadline ?? row.Rfq.ValidUntil;
                if (!string.IsNullOrWhiteSpace(bidDeadline) &&
                    DateTime.TryParse(bidDeadline, out var deadline))
                {
                    var diffTime = deadline - DateTime.UtcNow;
                    daysRemaining = (int)Math.Ceiling(diffTime.TotalDays);
                }

                var needsReply = (row.Rfq.Status == "published" || row.Rfq.Status == "in_progress") &&
                                 (row.QuoteStatus == null || row.QuoteStatus == "draft" || row.QuoteStatus == "withdrawn");

                var rfqDict = NodeCaseMapper.ToCamelCaseDictionary(row.Rfq);
                rfqDict["invitationStatus"] = row.Invitation.Status;
                rfqDict["invitationSentAt"] = row.Invitation.InvitedAt;
                rfqDict["quoteId"] = row.QuoteId;
                rfqDict["quoteStatus"] = row.QuoteStatus;
                rfqDict["quoteSubmittedAt"] = row.QuoteSubmittedAt;
                rfqDict["rfqStatus"] = row.Rfq.Status;
                rfqDict["validUntil"] = bidDeadline;
                rfqDict["daysRemaining"] = daysRemaining;
                rfqDict["needsResponse"] = needsReply;
                rfqDict["currentRound"] = row.BidRound == null
                    ? null
                    : await BuildBidRoundSummaryAsync(row.Rfq.Id, row.BidRound, null, true, cancellationToken);
                results.Add(rfqDict);
            }

            return Ok(new { data = results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch supplier invitations.");
            return StatusCode(500, new { message = "Failed to fetch invitations." });
        }
    }

}
