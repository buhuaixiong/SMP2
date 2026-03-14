using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Api.Controllers;

public sealed partial class RfqWorkflowController
{
    [HttpPost("create")]
    public async Task<IActionResult> CreateRfq([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var request = ParseRfqMutationRequest(body);
        if (request.ErrorResult != null)
        {
            return request.ErrorResult;
        }

        try
        {
            var user = HttpContext.GetAuthUser();
            var now = DateTime.UtcNow.ToString("o");

            await using var transaction = await _rfqWorkflowStore.BeginTransactionAsync(cancellationToken);

            var rfq = new Rfq
            {
                MaterialCategoryType = request.MaterialCategoryType,
                IsLineItemMode = true,
                Title = request.Title,
                Description = request.Description,
                RfqType = request.RfqType,
                DeliveryPeriod = request.DeliveryPeriod,
                BudgetAmount = request.BudgetAmount.HasValue && request.BudgetAmount.Value != 0 ? request.BudgetAmount : null,
                Currency = request.Currency,
                ValidUntil = request.ValidUntil,
                RequestingParty = request.RequestingParty,
                RequestingDepartment = request.RequestingDepartment,
                RequirementDate = request.RequirementDate,
                RequiredDocuments = request.RequiredDocumentsJson,
                EvaluationCriteria = request.EvaluationCriteriaJson,
                MinSupplierCount = request.MinSupplierCount,
                SupplierExceptionNote = request.SupplierExceptionNote,
                Status = "draft",
                CreatedBy = user!.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _rfqWorkflowStore.AddRfq(rfq);
            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            var initialBidRound = new RfqBidRound
            {
                RfqId = rfq.Id,
                RoundNumber = 1,
                BidDeadline = request.ValidUntil,
                Status = "draft",
                CreatedBy = user!.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _rfqWorkflowStore.AddBidRound(initialBidRound);
            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            AddRfqStructure(rfq.Id, initialBidRound.Id, request.LineItems, request.SupplierIds, request.ExternalInvitations, request.Currency, now);

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("rfq", rfq.Id.ToString(CultureInfo.InvariantCulture), "create",
                new
                {
                    request.Title,
                    request.MaterialCategoryType,
                    lineItemCount = request.LineItems.Count,
                    supplierCount = request.SupplierIds.Count + request.ExternalInvitations.Count,
                },
                user!,
                cancellationToken);

            var rfqData = await GetRfqWithLineItemsAsync(rfq.Id, cancellationToken);
            return StatusCode(201, new { message = "RFQ created successfully", data = rfqData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RFQ.");
            return StatusCode(500, new { message = "Failed to create RFQ." });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRfq(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var request = ParseRfqMutationRequest(body);
        if (request.ErrorResult != null)
        {
            return request.ErrorResult;
        }

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!string.Equals(rfq.Status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only draft RFQs can be updated." });
            }

            if (!string.Equals(rfq.CreatedBy, user?.Id, StringComparison.Ordinal))
            {
                return StatusCode(403, new { message = "Only the RFQ creator can update this RFQ." });
            }

            var now = DateTime.UtcNow.ToString("o");
            await using var transaction = await _rfqWorkflowStore.BeginTransactionAsync(cancellationToken);

            rfq.MaterialCategoryType = request.MaterialCategoryType;
            rfq.IsLineItemMode = true;
            rfq.Title = request.Title;
            rfq.Description = request.Description;
            rfq.RfqType = request.RfqType;
            rfq.DeliveryPeriod = request.DeliveryPeriod;
            rfq.BudgetAmount = request.BudgetAmount.HasValue && request.BudgetAmount.Value != 0 ? request.BudgetAmount : null;
            rfq.Currency = request.Currency;
            rfq.ValidUntil = request.ValidUntil;
            rfq.RequestingParty = request.RequestingParty;
            rfq.RequestingDepartment = request.RequestingDepartment;
            rfq.RequirementDate = request.RequirementDate;
            rfq.RequiredDocuments = request.RequiredDocumentsJson;
            rfq.EvaluationCriteria = request.EvaluationCriteriaJson;
            rfq.MinSupplierCount = request.MinSupplierCount;
            rfq.SupplierExceptionNote = request.SupplierExceptionNote;
            rfq.UpdatedAt = now;

            var initialBidRound = await _rfqWorkflowStore.FindLatestBidRoundAsync(rfq.Id, asNoTracking: false, cancellationToken)
                ?? new RfqBidRound
                {
                    RfqId = rfq.Id,
                    RoundNumber = 1,
                    CreatedBy = user?.Id,
                    CreatedAt = now,
                };

            if (initialBidRound.Id == 0)
            {
                _rfqWorkflowStore.AddBidRound(initialBidRound);
            }

            initialBidRound.BidDeadline = request.ValidUntil;
            initialBidRound.Status = "draft";
            initialBidRound.UpdatedAt = now;

            _rfqWorkflowStore.RemoveRfqLineItems(await _rfqWorkflowStore.LoadRfqLineItemsAsync(rfq.Id, cancellationToken));
            _rfqWorkflowStore.RemoveSupplierInvitations(await _rfqWorkflowStore.LoadSupplierInvitationsAsync(rfq.Id, cancellationToken));
            _rfqWorkflowStore.RemoveExternalInvitations(await _rfqWorkflowStore.LoadExternalInvitationsAsync(rfq.Id, cancellationToken));

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            AddRfqStructure(rfq.Id, initialBidRound.Id, request.LineItems, request.SupplierIds, request.ExternalInvitations, request.Currency, now);

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("rfq", rfq.Id.ToString(CultureInfo.InvariantCulture), "update",
                new
                {
                    request.Title,
                    request.MaterialCategoryType,
                    lineItemCount = request.LineItems.Count,
                    supplierCount = request.SupplierIds.Count + request.ExternalInvitations.Count,
                },
                user!,
                cancellationToken);

            var rfqData = await GetRfqWithLineItemsAsync(rfq.Id, cancellationToken);
            return Ok(new { message = "RFQ updated successfully", data = rfqData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update RFQ.");
            return StatusCode(500, new { message = "Failed to update RFQ." });
        }
    }

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> CloseRfq(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
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

        var reason = ReadStringValue(body, "reason");

        try
        {
            var rfq = await _rfqWorkflowStore.FindRfqAsync(id, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (string.Equals(rfq.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rfq.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "RFQ cannot be cancelled at this stage." });
            }

            rfq.Status = "cancelled";
            rfq.UpdatedAt = DateTime.UtcNow.ToString("o");

            var currentBidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(id, asNoTracking: false, cancellationToken);
            if (currentBidRound != null)
            {
                currentBidRound.Status = "cancelled";
                currentBidRound.ClosedAt = rfq.UpdatedAt;
                currentBidRound.UpdatedAt = rfq.UpdatedAt;
            }

            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "cancel", new { status = "cancelled", reason }, user, cancellationToken);
            }

            return Ok(new { message = "RFQ cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel RFQ.");
            return StatusCode(500, new { message = "Failed to cancel RFQ." });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRfq(int id, CancellationToken cancellationToken)
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

            if (!string.Equals(rfq.Status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only draft RFQs can be deleted." });
            }

            _rfqWorkflowStore.RemoveRfqLineItems(await _rfqWorkflowStore.LoadRfqLineItemsAsync(id, cancellationToken));
            _rfqWorkflowStore.RemoveSupplierInvitations(await _rfqWorkflowStore.LoadSupplierInvitationsAsync(id, cancellationToken));
            _rfqWorkflowStore.RemoveExternalInvitations(await _rfqWorkflowStore.LoadExternalInvitationsAsync(id, cancellationToken));
            _rfqWorkflowStore.RemoveRfq(rfq);
            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "delete", new { status = "deleted" }, user, cancellationToken);
            }

            return Ok(new { message = "RFQ deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete RFQ.");
            return StatusCode(500, new { message = "Failed to delete RFQ." });
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> PublishRfq(string id, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
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
            var rfq = await _rfqWorkflowStore.FindRfqAsync(rfqId, asNoTracking: false, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!string.Equals(rfq.Status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only draft RFQs can be published." });
            }

            if (string.IsNullOrWhiteSpace(rfq.ValidUntil))
            {
                return BadRequest(new { message = "Cannot publish RFQ without deadline." });
            }

            if (DateTime.TryParse(rfq.ValidUntil, out var deadline) && deadline < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Cannot publish RFQ with deadline in the past." });
            }

            var bidRound = await _rfqWorkflowStore.FindCurrentBidRoundAsync(rfqId, asNoTracking: false, cancellationToken)
                ?? new RfqBidRound
                {
                    RfqId = rfqId,
                    RoundNumber = 1,
                    CreatedBy = HttpContext.GetAuthUser()?.Id,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                };

            if (bidRound.Id == 0)
            {
                _rfqWorkflowStore.AddBidRound(bidRound);
            }

            rfq.Status = "published";
            rfq.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            bidRound.BidDeadline = rfq.ValidUntil;
            bidRound.Status = "published";
            bidRound.OpenedAt = null;
            bidRound.ClosedAt = null;
            bidRound.UpdatedAt = DateTime.UtcNow.ToString("o");
            await _rfqWorkflowStore.SaveChangesAsync(cancellationToken);

            await LogAuditAsync("rfq", rfqId.ToString(CultureInfo.InvariantCulture), "publish", new { status = "published" }, HttpContext.GetAuthUser()!, cancellationToken);
            await TryNotifyRfqPublishedAsync(rfq, cancellationToken);

            return Ok(new { message = "RFQ published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish RFQ.");
            return StatusCode(500, new { message = "Failed to publish RFQ." });
        }
    }

    private ParsedRfqMutationRequest ParseRfqMutationRequest(JsonElement body)
    {
        var materialCategoryType = JsonHelper.GetString(body, "materialCategoryType") ?? "IDM";
        var title = JsonHelper.GetString(body, "title");
        var description = JsonHelper.GetString(body, "description");
        var rfqType = JsonHelper.GetString(body, "rfqType");
        var deliveryPeriod = ReadStringValue(body, "deliveryPeriod")?.Trim();
        var budgetAmount = JsonHelper.GetDecimal(body, "budgetAmount");
        var currency = JsonHelper.GetString(body, "currency") ?? "CNY";
        var validUntil = JsonHelper.GetString(body, "validUntil");
        var requestingParty = JsonHelper.GetString(body, "requestingParty");
        var requestingDepartment = JsonHelper.GetString(body, "requestingDepartment");
        var requirementDate = JsonHelper.GetString(body, "requirementDate");
        var minSupplierCount = JsonHelper.GetInt(body, "minSupplierCount") ?? 3;
        var supplierExceptionNote = JsonHelper.GetString(body, "supplierExceptionNote");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(rfqType) || string.IsNullOrWhiteSpace(deliveryPeriod) || string.IsNullOrWhiteSpace(validUntil))
        {
            return ParsedRfqMutationRequest.WithError(BadRequest(new { message = "Title, RFQ type, delivery period and valid until are required." }));
        }

        if (string.Equals(materialCategoryType, "DM", StringComparison.OrdinalIgnoreCase))
        {
            return ParsedRfqMutationRequest.WithError(BadRequest(new { message = "DM material RFQs are not yet supported. Coming soon!" }));
        }

        if (!JsonHelper.TryGetProperty(body, "lineItems", out var lineItemsElement) || lineItemsElement.ValueKind != JsonValueKind.Array || lineItemsElement.GetArrayLength() == 0)
        {
            return ParsedRfqMutationRequest.WithError(BadRequest(new { message = "At least one line item is required." }));
        }

        var lineItems = lineItemsElement.EnumerateArray().ToList();
        var validation = ValidateRfqLineItems(lineItems);
        if (validation != null)
        {
            return ParsedRfqMutationRequest.WithError(validation);
        }

        var requiredDocumentsJson = JsonHelper.TryGetProperty(body, "requiredDocuments", out var requiredElement)
            ? requiredElement.GetRawText()
            : "[]";
        var evaluationCriteriaJson = JsonHelper.TryGetProperty(body, "evaluationCriteria", out var evaluationElement)
            ? evaluationElement.GetRawText()
            : "{}";

        return new ParsedRfqMutationRequest(
            materialCategoryType,
            title!,
            description,
            rfqType!,
            deliveryPeriod!,
            budgetAmount,
            currency,
            validUntil!,
            requestingParty,
            requestingDepartment,
            requirementDate,
            minSupplierCount,
            supplierExceptionNote,
            lineItems,
            ReadSupplierIds(body),
            ReadExternalInvitations(body),
            requiredDocumentsJson,
            evaluationCriteriaJson,
            null);
    }

    private IActionResult? ValidateRfqLineItems(IReadOnlyList<JsonElement> lineItems)
    {
        for (var i = 0; i < lineItems.Count; i++)
        {
            var item = lineItems[i];
            var materialCategory = JsonHelper.GetString(item, "materialCategory");
            var itemName = JsonHelper.GetString(item, "itemName");
            var quantity = JsonHelper.GetDecimal(item, "quantity");
            var unit = JsonHelper.GetString(item, "unit");

            if (string.IsNullOrWhiteSpace(materialCategory) || string.IsNullOrWhiteSpace(itemName) || quantity == null || string.IsNullOrWhiteSpace(unit))
            {
                return BadRequest(new { message = $"Line item {i + 1}: materialCategory, itemName, quantity and unit are required." });
            }
        }

        return null;
    }

    private static List<int> ReadSupplierIds(JsonElement body)
    {
        var supplierIds = new List<int>();
        if (JsonHelper.TryGetProperty(body, "supplierIds", out var supplierIdsElement) && supplierIdsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in supplierIdsElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var value))
                {
                    supplierIds.Add(value);
                }
            }
        }

        return supplierIds;
    }

    private static List<JsonElement> ReadExternalInvitations(JsonElement body)
    {
        var externalInvitations = new List<JsonElement>();
        if (JsonHelper.TryGetProperty(body, "externalEmails", out var externalEmailsElement) && externalEmailsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in externalEmailsElement.EnumerateArray())
            {
                externalInvitations.Add(entry);
            }
        }

        return externalInvitations;
    }

    private void AddRfqStructure(long rfqId, long? bidRoundId, IReadOnlyList<JsonElement> lineItems, IReadOnlyList<int> supplierIds, IReadOnlyList<JsonElement> externalInvitations, string currency, string now)
    {
        var lineNumber = 1;
        foreach (var item in lineItems)
        {
            _rfqWorkflowStore.AddRfqLineItem(new RfqLineItem
            {
                RfqId = rfqId,
                LineNumber = JsonHelper.GetInt(item, "lineNumber") ?? lineNumber,
                MaterialCategory = JsonHelper.GetString(item, "materialCategory"),
                Brand = JsonHelper.GetString(item, "brand"),
                ItemName = JsonHelper.GetString(item, "itemName"),
                Specifications = JsonHelper.GetString(item, "specifications"),
                Quantity = JsonHelper.GetDecimal(item, "quantity") ?? 0m,
                Unit = JsonHelper.GetString(item, "unit"),
                EstimatedUnitPrice = JsonHelper.GetDecimal(item, "estimatedUnitPrice"),
                Currency = JsonHelper.GetString(item, "currency") ?? currency,
                Parameters = JsonHelper.GetString(item, "parameters"),
                Notes = JsonHelper.GetString(item, "notes"),
                CreatedAt = now,
                Status = "draft",
                UpdatedAt = now,
            });
            lineNumber += 1;
        }

        foreach (var supplierId in supplierIds)
        {
            _rfqWorkflowStore.AddSupplierInvitation(new SupplierRfqInvitation
            {
                RfqId = (int)rfqId,
                BidRoundId = bidRoundId,
                SupplierId = supplierId,
                Status = "pending",
                InvitedAt = now,
                UpdatedAt = now,
            });
        }

        foreach (var external in externalInvitations)
        {
            var email = JsonHelper.GetString(external, "email");
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            _rfqWorkflowStore.AddExternalInvitation(new RfqExternalInvitation
            {
                RfqId = (int)rfqId,
                BidRoundId = bidRoundId,
                Email = email,
                CompanyName = JsonHelper.GetString(external, "companyName"),
                ContactPerson = JsonHelper.GetString(external, "contactPerson"),
                Status = "pending",
                InvitedAt = now,
                UpdatedAt = now,
            });
        }
    }

    private sealed record ParsedRfqMutationRequest(
        string MaterialCategoryType,
        string Title,
        string? Description,
        string RfqType,
        string DeliveryPeriod,
        decimal? BudgetAmount,
        string Currency,
        string ValidUntil,
        string? RequestingParty,
        string? RequestingDepartment,
        string? RequirementDate,
        int MinSupplierCount,
        string? SupplierExceptionNote,
        IReadOnlyList<JsonElement> LineItems,
        IReadOnlyList<int> SupplierIds,
        IReadOnlyList<JsonElement> ExternalInvitations,
        string RequiredDocumentsJson,
        string EvaluationCriteriaJson,
        IActionResult? ErrorResult)
    {
        public static ParsedRfqMutationRequest WithError(IActionResult errorResult)
        {
            return new ParsedRfqMutationRequest(string.Empty, string.Empty, null, string.Empty, string.Empty, null, string.Empty, string.Empty, null, null, null, 0, null, Array.Empty<JsonElement>(), Array.Empty<int>(), Array.Empty<JsonElement>(), string.Empty, string.Empty, errorResult);
        }
    }
}
