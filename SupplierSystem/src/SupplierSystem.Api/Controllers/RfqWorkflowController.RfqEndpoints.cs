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

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ListRfqs(CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(
            HttpContext.GetAuthUser(),
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove,
            Permissions.DepartmentRequisitionManage,
            Permissions.SupplierRfqShort,
            Permissions.SupplierRfqLong,
            Permissions.SupplierRfqReview);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var user = HttpContext.GetAuthUser();
        var page = ParseInt(Request.Query["page"], 1);
        var pageSize = ParseInt(Request.Query["pageSize"], ParseInt(Request.Query["limit"], 20));
        page = Math.Max(1, page);
        pageSize = Math.Min(100, Math.Max(1, pageSize));

        var keyword = GetSearchKeyword(Request.Query);
        var status = string.IsNullOrWhiteSpace(Request.Query["status"]) ? null : Request.Query["status"].ToString();
        var rfqType = string.IsNullOrWhiteSpace(Request.Query["rfqType"]) ? null : Request.Query["rfqType"].ToString();
        var materialType = string.IsNullOrWhiteSpace(Request.Query["materialType"]) ? null : Request.Query["materialType"].ToString();
        var distributionCategory = string.IsNullOrWhiteSpace(Request.Query["distributionCategory"]) ? null : Request.Query["distributionCategory"].ToString();
        var distributionSubcategory = string.IsNullOrWhiteSpace(Request.Query["distributionSubcategory"]) ? null : Request.Query["distributionSubcategory"].ToString();

        var query = _dbContext.Rfqs.AsNoTracking().AsQueryable();

        if (user != null)
        {
            var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var hasViewAll = granted.Contains(Permissions.RfqViewAll) ||
                             string.Equals(user.Role, "procurement_manager", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(user.Role, "procurement_director", StringComparison.OrdinalIgnoreCase);

            if (!hasViewAll)
            {
                if (user.SupplierId != null)
                {
                    var supplierId = user.SupplierId.Value;
                    var invitedRfqIds = await _dbContext.SupplierRfqInvitations.AsNoTracking()
                        .Where(inv => inv.SupplierId == supplierId)
                        .Select(inv => (long)inv.RfqId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                    query = query.Where(r => invitedRfqIds.Contains(r.Id));
                }
                else if (string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase))
                {
                    // Purchasers with rfq.short.manage can view all RFQs on the management page.
                }
                else
                {
                    query = query.Where(r => r.CreatedBy == user.Id);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(rfqType))
        {
            query = query.Where(r => r.RfqType == rfqType);
        }

        if (!string.IsNullOrWhiteSpace(materialType))
        {
            query = query.Where(r =>
                r.MaterialCategoryType == materialType ||
                r.MaterialType == materialType);
        }

        if (!string.IsNullOrWhiteSpace(distributionCategory))
        {
            query = query.Where(r => r.DistributionCategory == distributionCategory);
        }

        if (!string.IsNullOrWhiteSpace(distributionSubcategory))
        {
            query = query.Where(r => r.DistributionSubcategory == distributionSubcategory);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r => (r.Title ?? string.Empty).Contains(keyword) ||
                                     (r.Description ?? string.Empty).Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var data = rows.Select(NodeCaseMapper.ToCamelCaseDictionary).ToList();
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var response = new
        {
            data,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages,
                hasNext = page < totalPages,
                hasPrev = page > 1,
            },
        };

        return Ok(response);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateRfq([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequireAnyPermission(HttpContext.GetAuthUser(), Permissions.PurchaserRfqTarget);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var materialCategoryType = JsonHelper.GetString(body, "materialCategoryType") ?? "IDM";
        var title = JsonHelper.GetString(body, "title");
        var description = JsonHelper.GetString(body, "description");
        var rfqType = JsonHelper.GetString(body, "rfqType");
        var deliveryPeriod = ReadStringValue(body, "deliveryPeriod");
        if (!string.IsNullOrWhiteSpace(deliveryPeriod))
        {
            deliveryPeriod = deliveryPeriod.Trim();
        }
        var budgetAmount = JsonHelper.GetDecimal(body, "budgetAmount");
        var currency = JsonHelper.GetString(body, "currency") ?? "CNY";
        var validUntil = JsonHelper.GetString(body, "validUntil");
        var requestingParty = JsonHelper.GetString(body, "requestingParty");
        var requestingDepartment = JsonHelper.GetString(body, "requestingDepartment");
        var requirementDate = JsonHelper.GetString(body, "requirementDate");
        var minSupplierCount = JsonHelper.GetInt(body, "minSupplierCount") ?? 3;
        var supplierExceptionNote = JsonHelper.GetString(body, "supplierExceptionNote");

        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(rfqType) ||
            string.IsNullOrWhiteSpace(deliveryPeriod) ||
            string.IsNullOrWhiteSpace(validUntil))
        {
            return BadRequest(new { message = "Title, RFQ type, delivery period and valid until are required." });
        }

        if (string.Equals(materialCategoryType, "DM", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "DM material RFQs are not yet supported. Coming soon!" });
        }

        if (!JsonHelper.TryGetProperty(body, "lineItems", out var lineItemsElement) ||
            lineItemsElement.ValueKind != JsonValueKind.Array ||
            lineItemsElement.GetArrayLength() == 0)
        {
            return BadRequest(new { message = "At least one line item is required." });
        }

        var lineItems = new List<JsonElement>();
        foreach (var item in lineItemsElement.EnumerateArray())
        {
            lineItems.Add(item);
        }

        for (var i = 0; i < lineItems.Count; i++)
        {
            var item = lineItems[i];
            var materialCategory = JsonHelper.GetString(item, "materialCategory");
            var itemName = JsonHelper.GetString(item, "itemName");
            var quantity = JsonHelper.GetDecimal(item, "quantity");
            var unit = JsonHelper.GetString(item, "unit");

            if (string.IsNullOrWhiteSpace(materialCategory) ||
                string.IsNullOrWhiteSpace(itemName) ||
                quantity == null ||
                string.IsNullOrWhiteSpace(unit))
            {
                return BadRequest(new
                {
                    message = $"Line item {i + 1}: materialCategory, itemName, quantity and unit are required."
                });
            }
        }

        var supplierIds = new List<int>();
        if (JsonHelper.TryGetProperty(body, "supplierIds", out var supplierIdsElement) &&
            supplierIdsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in supplierIdsElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var value))
                {
                    supplierIds.Add(value);
                }
            }
        }

        var externalInvitations = new List<JsonElement>();
        if (JsonHelper.TryGetProperty(body, "externalEmails", out var externalEmailsElement) &&
            externalEmailsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in externalEmailsElement.EnumerateArray())
            {
                externalInvitations.Add(entry);
            }
        }

        var requiredDocumentsJson = JsonHelper.TryGetProperty(body, "requiredDocuments", out var requiredElement)
            ? requiredElement.GetRawText()
            : "[]";
        var evaluationCriteriaJson = JsonHelper.TryGetProperty(body, "evaluationCriteria", out var evaluationElement)
            ? evaluationElement.GetRawText()
            : "{}";

        try
        {
            var user = HttpContext.GetAuthUser();
            var now = DateTime.UtcNow.ToString("o");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var rfq = new Rfq
            {
                MaterialCategoryType = materialCategoryType,
                IsLineItemMode = true,
                Title = title,
                Description = description,
                RfqType = rfqType,
                DeliveryPeriod = deliveryPeriod,
                BudgetAmount = budgetAmount.HasValue && budgetAmount.Value != 0 ? budgetAmount : null,
                Currency = currency,
                ValidUntil = validUntil,
                RequestingParty = requestingParty,
                RequestingDepartment = requestingDepartment,
                RequirementDate = requirementDate,
                RequiredDocuments = requiredDocumentsJson,
                EvaluationCriteria = evaluationCriteriaJson,
                MinSupplierCount = minSupplierCount,
                SupplierExceptionNote = supplierExceptionNote,
                Status = "draft",
                CreatedBy = user!.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _dbContext.Rfqs.Add(rfq);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var lineNumber = 1;
            foreach (var item in lineItems)
            {
                var lineItem = new RfqLineItem
                {
                    RfqId = rfq.Id,
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
                };
                _dbContext.RfqLineItems.Add(lineItem);
                lineNumber += 1;
            }

            if (supplierIds.Count > 0)
            {
                foreach (var supplierId in supplierIds)
                {
                    _dbContext.SupplierRfqInvitations.Add(new SupplierRfqInvitation
                    {
                        RfqId = (int)rfq.Id,
                        SupplierId = supplierId,
                        Status = "pending",
                        InvitedAt = now,
                        UpdatedAt = now,
                    });
                }
            }

            if (externalInvitations.Count > 0)
            {
                foreach (var external in externalInvitations)
                {
                    var email = JsonHelper.GetString(external, "email");
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        continue;
                    }

                    _dbContext.RfqExternalInvitations.Add(new RfqExternalInvitation
                    {
                        RfqId = (int)rfq.Id,
                        Email = email,
                        CompanyName = JsonHelper.GetString(external, "companyName"),
                        ContactPerson = JsonHelper.GetString(external, "contactPerson"),
                        Status = "pending",
                        InvitedAt = now,
                        UpdatedAt = now,
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("rfq", rfq.Id.ToString(CultureInfo.InvariantCulture), "create",
                new
                {
                    title,
                    materialCategoryType,
                    lineItemCount = lineItems.Count,
                    supplierCount = supplierIds.Count + externalInvitations.Count,
                },
                user!,
                cancellationToken);

            var rfqData = await GetRfqWithLineItemsAsync(rfq.Id, cancellationToken);
            return StatusCode(201, new
            {
                message = "RFQ created successfully",
                data = rfqData,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create RFQ.");
            return StatusCode(500, new { message = "Failed to create RFQ." });
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

            var invitedSuppliers = await (from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
                                          join s in _dbContext.Suppliers.AsNoTracking()
                                              on inv.SupplierId equals s.Id
                                          where inv.RfqId == rfqId
                                          select new
                                          {
                                              s.Id,
                                              s.CompanyId,
                                              s.SupplierCode,
                                              VendorCode = s.SupplierCode ?? s.CompanyId,
                                              s.CompanyName,
                                              s.Stage,
                                              InvitationStatus = inv.Status
                                          })
                .ToListAsync(cancellationToken);

            var invitedSupplierList = invitedSuppliers.Select(s => new Dictionary<string, object?>
            {
                ["id"] = s.Id,
                ["companyId"] = s.CompanyId,
                ["supplierCode"] = s.SupplierCode,
                ["vendorCode"] = s.VendorCode,
                ["companyName"] = s.CompanyName,
                ["stage"] = s.Stage,
                ["invitationStatus"] = s.InvitationStatus,
            }).ToList();

            var externalInvitations = await _dbContext.RfqExternalInvitations.AsNoTracking()
                .Where(inv => inv.RfqId == rfqId)
                .ToListAsync(cancellationToken);

            var externalInvitationList = externalInvitations
                .Select(NodeCaseMapper.ToSnakeCaseDictionary)
                .ToList();

            var quoteRows = await (from q in _dbContext.Quotes.AsNoTracking()
                                   join s in _dbContext.Suppliers.AsNoTracking()
                                       on q.SupplierId equals s.Id
                                   where q.RfqId == rfqId
                                   orderby q.SubmittedAt descending
                                   select new
                                   {
                                       Quote = q,
                                       s.CompanyName,
                                       s.CompanyId
                                   })
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

            var visibility = await QuoteVisibility.GetVisibilityAsync(_dbContext, rfqId, user, cancellationToken);
            if (visibility.Locked)
            {
                quotes = new List<Dictionary<string, object?>>();
            }

            var priceComparisons = await _dbContext.PriceComparisonAttachments.AsNoTracking()
                .Where(att => att.RfqId == rfqId)
                .ToListAsync(cancellationToken);

            var priceComparisonList = priceComparisons.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();

            var approvals = await _dbContext.RfqApprovals.AsNoTracking()
                .Where(a => a.RfqId == rfqId)
                .OrderBy(a => a.StepOrder)
                .ToListAsync(cancellationToken);

            var approvalList = approvals.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();

            var prRecord = await _dbContext.RfqPrRecords.AsNoTracking()
                .FirstOrDefaultAsync(pr => pr.RfqId == rfqId, cancellationToken);

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
            _logger.LogError(ex, "Failed to fetch RFQ.");
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
            var rfq = await _dbContext.Rfqs.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var invitation = await _dbContext.SupplierRfqInvitations.AsNoTracking()
                .FirstOrDefaultAsync(i => i.RfqId == id && i.SupplierId == user.SupplierId, cancellationToken);
            if (invitation == null)
            {
                return StatusCode(403, new { message = "You are not invited to this RFQ." });
            }

            var rfqData = await GetRfqWithLineItemsAsync(id, cancellationToken);
            if (rfqData == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            var quoteRows = await (from q in _dbContext.Quotes.AsNoTracking()
                                   where q.RfqId == id && q.SupplierId == user.SupplierId
                                   orderby q.SubmittedAt descending
                                   select q)
                .ToListAsync(cancellationToken);

            var quotes = new List<Dictionary<string, object?>>();
            foreach (var quote in quoteRows)
            {
                quotes.Add(await BuildQuoteResponseAsync(quote, quote.Currency ?? DefaultCurrency, cancellationToken));
            }

            int? daysRemaining = null;
            if (!string.IsNullOrWhiteSpace(rfq.ValidUntil) &&
                DateTime.TryParse(rfq.ValidUntil, out var deadline))
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
            invitationDict["validUntil"] = rfq.ValidUntil;
            invitationDict["daysRemaining"] = daysRemaining;
            invitationDict["needsResponse"] = needsResponse;

            rfqData["quotes"] = quotes;
            rfqData["supplierInvitation"] = invitationDict;
            rfqData["invitation"] = invitationDict;

            return Ok(new { data = rfqData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch supplier RFQ.");
            return StatusCode(500, new { message = "Failed to fetch RFQ." });
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

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid RFQ ID." });
        }

        var materialCategoryType = JsonHelper.GetString(body, "materialCategoryType") ?? "IDM";
        var title = JsonHelper.GetString(body, "title");
        var description = JsonHelper.GetString(body, "description");
        var rfqType = JsonHelper.GetString(body, "rfqType");
        var deliveryPeriod = ReadStringValue(body, "deliveryPeriod");
        if (!string.IsNullOrWhiteSpace(deliveryPeriod))
        {
            deliveryPeriod = deliveryPeriod.Trim();
        }
        var budgetAmount = JsonHelper.GetDecimal(body, "budgetAmount");
        var currency = JsonHelper.GetString(body, "currency") ?? "CNY";
        var validUntil = JsonHelper.GetString(body, "validUntil");
        var requestingParty = JsonHelper.GetString(body, "requestingParty");
        var requestingDepartment = JsonHelper.GetString(body, "requestingDepartment");
        var requirementDate = JsonHelper.GetString(body, "requirementDate");
        var minSupplierCount = JsonHelper.GetInt(body, "minSupplierCount") ?? 3;
        var supplierExceptionNote = JsonHelper.GetString(body, "supplierExceptionNote");

        if (string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(rfqType) ||
            string.IsNullOrWhiteSpace(deliveryPeriod) ||
            string.IsNullOrWhiteSpace(validUntil))
        {
            return BadRequest(new { message = "Title, RFQ type, delivery period and valid until are required." });
        }

        if (string.Equals(materialCategoryType, "DM", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "DM material RFQs are not yet supported. Coming soon!" });
        }

        if (!JsonHelper.TryGetProperty(body, "lineItems", out var lineItemsElement) ||
            lineItemsElement.ValueKind != JsonValueKind.Array ||
            lineItemsElement.GetArrayLength() == 0)
        {
            return BadRequest(new { message = "At least one line item is required." });
        }

        var lineItems = new List<JsonElement>();
        foreach (var item in lineItemsElement.EnumerateArray())
        {
            lineItems.Add(item);
        }

        for (var i = 0; i < lineItems.Count; i++)
        {
            var item = lineItems[i];
            var materialCategory = JsonHelper.GetString(item, "materialCategory");
            var itemName = JsonHelper.GetString(item, "itemName");
            var quantity = JsonHelper.GetDecimal(item, "quantity");
            var unit = JsonHelper.GetString(item, "unit");

            if (string.IsNullOrWhiteSpace(materialCategory) ||
                string.IsNullOrWhiteSpace(itemName) ||
                quantity == null ||
                string.IsNullOrWhiteSpace(unit))
            {
                return BadRequest(new
                {
                    message = $"Line item {i + 1}: materialCategory, itemName, quantity and unit are required."
                });
            }
        }

        var supplierIds = new List<int>();
        if (JsonHelper.TryGetProperty(body, "supplierIds", out var supplierIdsElement) &&
            supplierIdsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in supplierIdsElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var value))
                {
                    supplierIds.Add(value);
                }
            }
        }

        var externalInvitations = new List<JsonElement>();
        if (JsonHelper.TryGetProperty(body, "externalEmails", out var externalEmailsElement) &&
            externalEmailsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in externalEmailsElement.EnumerateArray())
            {
                externalInvitations.Add(entry);
            }
        }

        var requiredDocumentsJson = JsonHelper.TryGetProperty(body, "requiredDocuments", out var requiredElement)
            ? requiredElement.GetRawText()
            : "[]";
        var evaluationCriteriaJson = JsonHelper.TryGetProperty(body, "evaluationCriteria", out var evaluationElement)
            ? evaluationElement.GetRawText()
            : "{}";

        try
        {
            var user = HttpContext.GetAuthUser();
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
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

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            rfq.MaterialCategoryType = materialCategoryType;
            rfq.IsLineItemMode = true;
            rfq.Title = title;
            rfq.Description = description;
            rfq.RfqType = rfqType;
            rfq.DeliveryPeriod = deliveryPeriod;
            rfq.BudgetAmount = budgetAmount.HasValue && budgetAmount.Value != 0 ? budgetAmount : null;
            rfq.Currency = currency;
            rfq.ValidUntil = validUntil;
            rfq.RequestingParty = requestingParty;
            rfq.RequestingDepartment = requestingDepartment;
            rfq.RequirementDate = requirementDate;
            rfq.RequiredDocuments = requiredDocumentsJson;
            rfq.EvaluationCriteria = evaluationCriteriaJson;
            rfq.MinSupplierCount = minSupplierCount;
            rfq.SupplierExceptionNote = supplierExceptionNote;
            rfq.UpdatedAt = now;

            var existingLineItems = await _dbContext.RfqLineItems
                .Where(li => li.RfqId == rfq.Id)
                .ToListAsync(cancellationToken);
            _dbContext.RfqLineItems.RemoveRange(existingLineItems);

            var lineNumber = 1;
            foreach (var item in lineItems)
            {
                var lineItem = new RfqLineItem
                {
                    RfqId = rfq.Id,
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
                };
                _dbContext.RfqLineItems.Add(lineItem);
                lineNumber += 1;
            }

            var existingInvitations = await _dbContext.SupplierRfqInvitations
                .Where(inv => inv.RfqId == rfq.Id)
                .ToListAsync(cancellationToken);
            _dbContext.SupplierRfqInvitations.RemoveRange(existingInvitations);

            var existingExternalInvitations = await _dbContext.RfqExternalInvitations
                .Where(inv => inv.RfqId == rfq.Id)
                .ToListAsync(cancellationToken);
            _dbContext.RfqExternalInvitations.RemoveRange(existingExternalInvitations);

            if (supplierIds.Count > 0)
            {
                foreach (var supplierId in supplierIds)
                {
                    _dbContext.SupplierRfqInvitations.Add(new SupplierRfqInvitation
                    {
                        RfqId = (int)rfq.Id,
                        SupplierId = supplierId,
                        Status = "pending",
                        InvitedAt = now,
                        UpdatedAt = now,
                    });
                }
            }

            if (externalInvitations.Count > 0)
            {
                foreach (var external in externalInvitations)
                {
                    var email = JsonHelper.GetString(external, "email");
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        continue;
                    }

                    _dbContext.RfqExternalInvitations.Add(new RfqExternalInvitation
                    {
                        RfqId = (int)rfq.Id,
                        Email = email,
                        CompanyName = JsonHelper.GetString(external, "companyName"),
                        ContactPerson = JsonHelper.GetString(external, "contactPerson"),
                        Status = "pending",
                        InvitedAt = now,
                        UpdatedAt = now,
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await LogAuditAsync("rfq", rfq.Id.ToString(CultureInfo.InvariantCulture), "update",
                new
                {
                    title,
                    materialCategoryType,
                    lineItemCount = lineItems.Count,
                    supplierCount = supplierIds.Count + externalInvitations.Count,
                },
                user!,
                cancellationToken);

            var rfqData = await GetRfqWithLineItemsAsync(rfq.Id, cancellationToken);
            return Ok(new
            {
                message = "RFQ updated successfully",
                data = rfqData,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update RFQ.");
            return StatusCode(500, new { message = "Failed to update RFQ." });
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

            var query = from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
                        join rfq in _dbContext.Rfqs.AsNoTracking() on (long)inv.RfqId equals rfq.Id
                        join quote in _dbContext.Quotes.AsNoTracking()
                            .Where(q => q.SupplierId == supplierId && q.IsLatest)
                            on (long)inv.RfqId equals quote.RfqId into quoteGroup
                        from quote in quoteGroup.DefaultIfEmpty()
                        where inv.SupplierId == supplierId
                        select new
                        {
                            rfq,
                            inv,
                            quoteId = (long?)quote.Id,
                            quoteStatus = quote.Status,
                            quoteSubmittedAt = quote.SubmittedAt,
                        };

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(row => row.inv.Status == status);
            }

            if (needsResponse)
            {
                query = query.Where(row =>
                    (row.rfq.Status == "published" || row.rfq.Status == "in_progress") &&
                    (row.quoteStatus == null || row.quoteStatus == "draft" || row.quoteStatus == "withdrawn"));
            }

            var rows = await query
                .OrderByDescending(row => row.inv.InvitedAt)
                .ToListAsync(cancellationToken);

            var results = new List<Dictionary<string, object?>>();
            foreach (var row in rows)
            {
                int? daysRemaining = null;
                if (!string.IsNullOrWhiteSpace(row.rfq.ValidUntil) &&
                    DateTime.TryParse(row.rfq.ValidUntil, out var deadline))
                {
                    var diffTime = deadline - DateTime.UtcNow;
                    daysRemaining = (int)Math.Ceiling(diffTime.TotalDays);
                }

                var needsReply = (row.rfq.Status == "published" || row.rfq.Status == "in_progress") &&
                                 (row.quoteStatus == null || row.quoteStatus == "draft" || row.quoteStatus == "withdrawn");

                var rfqDict = NodeCaseMapper.ToCamelCaseDictionary(row.rfq);
                rfqDict["invitationStatus"] = row.inv.Status;
                rfqDict["invitationSentAt"] = row.inv.InvitedAt;
                rfqDict["quoteId"] = row.quoteId;
                rfqDict["quoteStatus"] = row.quoteStatus;
                rfqDict["quoteSubmittedAt"] = row.quoteSubmittedAt;
                rfqDict["rfqStatus"] = row.rfq.Status;
                rfqDict["daysRemaining"] = daysRemaining;
                rfqDict["needsResponse"] = needsReply;
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
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
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

            await _dbContext.SaveChangesAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "cancel",
                    new { status = "cancelled", reason }, user, cancellationToken);
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
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rfq == null)
            {
                return NotFound(new { message = "RFQ not found." });
            }

            if (!string.Equals(rfq.Status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only draft RFQs can be deleted." });
            }

            var lineItems = await _dbContext.RfqLineItems
                .Where(li => li.RfqId == id)
                .ToListAsync(cancellationToken);
            _dbContext.RfqLineItems.RemoveRange(lineItems);

            var invitations = await _dbContext.SupplierRfqInvitations
                .Where(inv => inv.RfqId == id)
                .ToListAsync(cancellationToken);
            _dbContext.SupplierRfqInvitations.RemoveRange(invitations);

            var externalInvitations = await _dbContext.RfqExternalInvitations
                .Where(inv => inv.RfqId == id)
                .ToListAsync(cancellationToken);
            _dbContext.RfqExternalInvitations.RemoveRange(externalInvitations);

            _dbContext.Rfqs.Remove(rfq);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var user = HttpContext.GetAuthUser();
            if (user != null)
            {
                await LogAuditAsync("rfq", id.ToString(CultureInfo.InvariantCulture), "delete",
                    new { status = "deleted" }, user, cancellationToken);
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
            var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
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

            rfq.Status = "published";
            rfq.UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            await _dbContext.SaveChangesAsync(cancellationToken);

            await LogAuditAsync("rfq", rfqId.ToString(CultureInfo.InvariantCulture), "publish",
                new { status = "published" }, HttpContext.GetAuthUser()!, cancellationToken);

            await TryNotifyRfqPublishedAsync(rfq, cancellationToken);

            return Ok(new { message = "RFQ published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish RFQ.");
            return StatusCode(500, new { message = "Failed to publish RFQ." });
        }
    }
}
