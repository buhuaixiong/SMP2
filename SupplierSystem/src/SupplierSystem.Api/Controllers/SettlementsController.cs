using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settlements")]
public sealed class SettlementsController : ControllerBase
{
    private static readonly string[] AccountantStatuses =
    {
        "draft",
        "pending_approval",
        "approved",
    };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<SettlementsController> _logger;

    public SettlementsController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        ILogger<SettlementsController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> ListSettlements(CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "finance_director", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var type = Request.Query["type"].ToString();
        var status = Request.Query["status"].ToString();
        var page = ParseInt(Request.Query["page"], 1);
        var limit = ParseInt(Request.Query["limit"], 20);
        if (!TryReadIntFromQuery(Request.Query, out var supplierId, "supplier_id", "supplierId"))
        {
            return BadRequest(new { message = "supplierId must be a valid integer." });
        }

        page = Math.Max(1, page);
        limit = Math.Min(Math.Max(1, limit), 100);

        var role = ControllerHelpers.NormalizeRole(user?.Role) ?? string.Empty;

        var query = from settlement in _dbContext.Settlements.AsNoTracking()
                    join supplier in _dbContext.Suppliers.AsNoTracking()
                        on settlement.SupplierId equals supplier.Id into supplierGroup
                    from supplier in supplierGroup.DefaultIfEmpty()
                    join rfq in _dbContext.Rfqs.AsNoTracking()
                        on (long?)settlement.RfqId equals rfq.Id into rfqGroup
                    from rfq in rfqGroup.DefaultIfEmpty()
                    join creator in _dbContext.Users.AsNoTracking()
                        on settlement.CreatedBy equals creator.Id into creatorGroup
                    from creator in creatorGroup.DefaultIfEmpty()
                    select new
                    {
                        Settlement = settlement,
                        SupplierName = supplier.CompanyName,
                        SupplierStage = supplier.Stage,
                        CreatorName = creator.Name,
                        RfqTitle = rfq.Title
                    };

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(item => item.Settlement.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Settlement.Status == status);
        }

        if (supplierId.HasValue)
        {
            query = query.Where(item => item.Settlement.SupplierId == supplierId.Value);
        }

        if (string.Equals(role, "finance_accountant", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(item =>
                item.Settlement.Status != null && AccountantStatuses.Contains(item.Settlement.Status));
        }

        var settlements = await query
            .OrderByDescending(item => item.Settlement.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(item => new
            {
                item.Settlement.Id,
                item.Settlement.StatementNumber,
                item.Settlement.SupplierId,
                item.Settlement.RfqId,
                item.Settlement.Type,
                item.Settlement.PeriodStart,
                item.Settlement.PeriodEnd,
                item.Settlement.TotalInvoices,
                item.Settlement.TotalAmount,
                item.Settlement.TaxAmount,
                item.Settlement.GrandTotal,
                item.Settlement.Status,
                item.Settlement.CreatedBy,
                item.Settlement.CreatedAt,
                item.Settlement.ReviewerId,
                item.Settlement.ReviewedAt,
                item.Settlement.ReviewNotes,
                item.Settlement.RejectionReason,
                item.Settlement.DirectorApproved,
                item.Settlement.DirectorApproverId,
                item.Settlement.DirectorApprovedAt,
                item.Settlement.DirectorApprovalNotes,
                item.Settlement.ExceptionalReason,
                item.Settlement.PaymentDueDate,
                item.Settlement.PaidDate,
                item.Settlement.DisputeReceived,
                item.Settlement.DisputeReason,
                item.Settlement.DisputedItems,
                item.Settlement.SupportingDocuments,
                item.Settlement.DisputeProcessorId,
                item.Settlement.DisputeReceivedAt,
                item.Settlement.Details,
                item.SupplierName,
                item.SupplierStage,
                item.CreatorName,
                item.RfqTitle
            })
            .ToListAsync(cancellationToken);

        return Ok(new { data = settlements });
    }

    [HttpPost("monthly-statements")]
    public async Task<IActionResult> CreateMonthlyStatements([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var year = ReadInt(body, "year");
        var month = ReadInt(body, "month");
        if (!year.HasValue || !month.HasValue)
        {
            return BadRequest(new { message = "year and month are required." });
        }

        if (month < 1 || month > 12)
        {
            return BadRequest(new { message = "month must be between 1 and 12." });
        }

        var startDate = new DateTime(year.Value, month.Value, 1);
        var endDate = startDate.AddMonths(1);

        var supplierIds = ReadIntArray(body, "supplier_ids", "supplierIds");
        var suppliersQuery = _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.Stage == "formal" && s.Status == "active");

        if (supplierIds.Count > 0)
        {
            suppliersQuery = suppliersQuery.Where(s => supplierIds.Contains(s.Id));
        }

        var suppliers = await suppliersQuery
            .Select(s => new
            {
                s.Id,
                s.CompanyName,
                s.BankAccount,
                s.ContactEmail
            })
            .ToListAsync(cancellationToken);

        if (suppliers.Count == 0)
        {
            return Ok(new
            {
                message = "No eligible suppliers found.",
                results = Array.Empty<object>(),
                failed_count = 0
            });
        }

        var results = new List<object>();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var createdAt = DateTime.UtcNow;
        var createdBy = user?.Id;

        foreach (var supplier in suppliers)
        {
            try
            {
                var invoices = await (from invoice in _dbContext.Invoices.AsNoTracking()
                                      join rfq in _dbContext.Rfqs.AsNoTracking()
                                          on (long?)invoice.RfqId equals rfq.Id into rfqGroup
                                      from rfq in rfqGroup.DefaultIfEmpty()
                                      where invoice.SupplierId == supplier.Id
                                            && invoice.Status == "verified"
                                            && invoice.InvoiceDate >= startDate
                                            && invoice.InvoiceDate < endDate
                                      orderby invoice.InvoiceDate
                                      select new
                                      {
                                          invoice.Id,
                                          invoice.InvoiceNumber,
                                          invoice.Amount,
                                          invoice.TaxRate,
                                          invoice.InvoiceDate,
                                          invoice.RfqId,
                                          RfqTitle = rfq.Title
                                      })
                    .ToListAsync(cancellationToken);

                var totalInvoices = invoices.Count;
                var totalAmount = invoices.Sum(i => i.Amount);
                var taxAmount = 0m;
                foreach (var invoice in invoices)
                {
                    var rate = ParseTaxRate(invoice.TaxRate);
                    if (rate <= 0)
                    {
                        continue;
                    }

                    taxAmount += invoice.Amount * rate;
                }

                var grandTotal = totalAmount + taxAmount;
                var statementNumber = $"STM-{year:0000}{month:00}-{supplier.Id}";

                var details = new
                {
                    invoices = invoices.Select(i => new
                    {
                        id = i.Id,
                        invoice_number = i.InvoiceNumber,
                        amount = i.Amount,
                        invoice_date = i.InvoiceDate.ToString("yyyy-MM-dd"),
                        rfq_id = i.RfqId,
                        rfq_title = i.RfqTitle
                    }),
                    supplier_info = new
                    {
                        name = supplier.CompanyName,
                        bank_account = supplier.BankAccount,
                        contact_email = supplier.ContactEmail
                    }
                };

                var settlement = new Settlement
                {
                    StatementNumber = statementNumber,
                    SupplierId = supplier.Id,
                    Type = "monthly",
                    PeriodStart = startDate.ToString("yyyy-MM-dd"),
                    PeriodEnd = endDate.ToString("yyyy-MM-dd"),
                    TotalInvoices = totalInvoices,
                    TotalAmount = totalAmount,
                    TaxAmount = taxAmount,
                    GrandTotal = grandTotal,
                    Status = "draft",
                    CreatedBy = createdBy,
                    CreatedAt = createdAt,
                    Details = JsonSerializer.Serialize(details)
                };

                _dbContext.Settlements.Add(settlement);
                await _dbContext.SaveChangesAsync(cancellationToken);

                results.Add(new
                {
                    statement_number = statementNumber,
                    supplier_name = supplier.CompanyName,
                    total_invoices = totalInvoices,
                    grand_total = grandTotal,
                    settlement_id = settlement.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Settlements] Failed to generate statement for supplier {SupplierId}.", supplier.Id);
            }
        }

        return Ok(new
        {
            message = $"Successfully generated {results.Count} supplier statement(s).",
            results,
            failed_count = suppliers.Count - results.Count
        });
    }

    [HttpPost("pre-payment/{id:int}/review")]
    public async Task<IActionResult> ReviewPrePayment(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var status = (ReadString(body, "status") ?? string.Empty).Trim().ToLowerInvariant();
        var reviewNotes = ReadString(body, "review_notes", "reviewNotes");
        var rejectionReason = ReadString(body, "rejection_reason", "rejectionReason");

        if (status != "approved" && status != "rejected")
        {
            return BadRequest(new { message = "status must be approved or rejected." });
        }

        var settlement = await _dbContext.Settlements
            .FirstOrDefaultAsync(s => s.Id == id && s.Type == "pre_payment", cancellationToken);

        if (settlement == null)
        {
            return NotFound(new { message = "Pre-payment settlement not found." });
        }

        if (!settlement.SupplierId.HasValue)
        {
            return BadRequest(new { message = "Settlement supplier is missing." });
        }

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == settlement.SupplierId.Value, cancellationToken);

        if (supplier == null)
        {
            return BadRequest(new { message = "Supplier not found." });
        }

        if (!string.Equals(supplier.Stage, "temporary", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only temporary suppliers can request pre-payment." });
        }

        var rfqAmount = await GetRfqAmountAsync(settlement.RfqId, cancellationToken);
        if (!rfqAmount.HasValue || rfqAmount.Value <= 0)
        {
            return BadRequest(new { message = "RFQ amount is required for pre-payment review." });
        }

        var settlementAmount = GetSettlementAmount(settlement);
        var prePaymentRatio = settlementAmount == 0 ? 0 : settlementAmount / rfqAmount.Value * 100m;
        if (prePaymentRatio > 30m)
        {
            return BadRequest(new
            {
                message = $"Pre-payment ratio {prePaymentRatio:0.##}% exceeds the 30% threshold.",
                current_ratio = prePaymentRatio
            });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        var overdueCount = await _dbContext.Settlements.AsNoTracking()
            .Where(s => s.SupplierId == settlement.SupplierId
                        && s.Status == "approved"
                        && s.PaymentDueDate != null
                        && string.Compare(s.PaymentDueDate, now) < 0)
            .CountAsync(cancellationToken);

        if (overdueCount > 0)
        {
            return BadRequest(new
            {
                message = "Supplier has overdue settlements; pre-payment cannot be approved.",
                overdue_count = overdueCount
            });
        }

        settlement.Status = status;
        settlement.ReviewerId = TryParseUserId(user);
        settlement.ReviewedAt = now;
        settlement.ReviewNotes = reviewNotes;
        settlement.RejectionReason = rejectionReason;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(user, $"pre_payment_review_{status}", settlement.Id.ToString(), new
        {
            pre_payment_ratio = prePaymentRatio,
            review_notes = reviewNotes,
            rejection_reason = rejectionReason
        });

        return Ok(new
        {
            message = "Pre-payment review completed.",
            status,
            pre_payment_ratio = prePaymentRatio
        });
    }

    [HttpPost("pre-payment/{id:int}/director-approval")]
    public async Task<IActionResult> DirectorApproval(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_director", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var approvalStatus = ReadBool(body, "approval_status", "approvalStatus");
        var approvalNotes = ReadString(body, "approval_notes", "approvalNotes");
        var exceptionalReason = ReadString(body, "exceptional_reason", "exceptionalReason");

        if (!approvalStatus.HasValue)
        {
            return BadRequest(new { message = "approval_status is required." });
        }

        if (string.IsNullOrWhiteSpace(exceptionalReason))
        {
            return BadRequest(new { message = "exceptional_reason is required for director approval." });
        }

        var settlement = await _dbContext.Settlements
            .FirstOrDefaultAsync(s => s.Id == id && s.Type == "pre_payment", cancellationToken);

        if (settlement == null)
        {
            return NotFound(new { message = "Pre-payment settlement not found." });
        }

        if (!settlement.SupplierId.HasValue)
        {
            return BadRequest(new { message = "Settlement supplier is missing." });
        }

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == settlement.SupplierId.Value, cancellationToken);

        if (supplier == null)
        {
            return BadRequest(new { message = "Supplier not found." });
        }

        var rfqAmount = await GetRfqAmountAsync(settlement.RfqId, cancellationToken);
        if (!rfqAmount.HasValue || rfqAmount.Value <= 0)
        {
            return BadRequest(new { message = "RFQ amount is required for director approval." });
        }

        var settlementAmount = GetSettlementAmount(settlement);
        var prePaymentRatio = settlementAmount == 0 ? 0 : settlementAmount / rfqAmount.Value * 100m;
        var isExceptional = prePaymentRatio > 30m ||
                            (string.Equals(supplier.Stage, "formal", StringComparison.OrdinalIgnoreCase)
                             && prePaymentRatio > 50m);

        if (!isExceptional)
        {
            return BadRequest(new
            {
                message = "This pre-payment does not require exceptional approval.",
                current_ratio = prePaymentRatio
            });
        }

        var now = DateTimeOffset.UtcNow.ToString("o");
        settlement.DirectorApproved = approvalStatus.Value;
        settlement.DirectorApproverId = TryParseUserId(user);
        settlement.DirectorApprovedAt = now;
        settlement.DirectorApprovalNotes = approvalNotes;
        settlement.ExceptionalReason = exceptionalReason;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(user, $"exceptional_pre_payment_approval_{approvalStatus.Value}", settlement.Id.ToString(), new
        {
            pre_payment_ratio = prePaymentRatio,
            approval_notes = approvalNotes,
            exceptional_reason = exceptionalReason
        });

        return Ok(new
        {
            message = "Exceptional pre-payment approval completed.",
            approval_status = approvalStatus.Value,
            pre_payment_ratio = prePaymentRatio
        });
    }

    [HttpGet("progress-tracking")]
    public async Task<IActionResult> GetProgressTracking(CancellationToken cancellationToken)
    {
        var permissionResult = RequireRole(HttpContext.GetAuthUser(), "finance_accountant", "finance_director", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var nowText = DateTimeOffset.UtcNow.ToString("o");
        var threshold = DateTime.UtcNow.AddDays(-30);

        var recentQuery = _dbContext.Settlements.AsNoTracking()
            .Where(s => s.CreatedAt.HasValue && s.CreatedAt.Value >= threshold);

        var total = await recentQuery.CountAsync(cancellationToken);
        var draftCount = await recentQuery.CountAsync(s => s.Status == "draft", cancellationToken);
        var pendingCount = await recentQuery.CountAsync(s => s.Status == "pending_approval", cancellationToken);
        var approvedCount = await recentQuery.CountAsync(s => s.Status == "approved", cancellationToken);
        var paidCount = await recentQuery.CountAsync(s => s.Status == "paid", cancellationToken);
        var archivedCount = await recentQuery.CountAsync(s => s.Status == "archived", cancellationToken);
        var prePaymentCount = await recentQuery.CountAsync(s => s.Type == "pre_payment", cancellationToken);
        var monthlyCount = await recentQuery.CountAsync(s => s.Type == "monthly", cancellationToken);
        var quarterlyCount = await recentQuery.CountAsync(s => s.Type == "quarterly", cancellationToken);

        var overdueCount = await _dbContext.Settlements.AsNoTracking()
            .Where(s => s.Status == "approved"
                        && s.PaymentDueDate != null
                        && s.CreatedAt.HasValue
                        && s.CreatedAt.Value >= threshold
                        && string.Compare(s.PaymentDueDate, nowText) < 0)
            .CountAsync(cancellationToken);

        var completionRate = total == 0
            ? 0
            : Math.Round((paidCount + archivedCount) / (double)total * 100, 2, MidpointRounding.AwayFromZero);

        return Ok(new
        {
            data = new
            {
                total_settlements = total,
                draft_count = draftCount,
                pending_approval_count = pendingCount,
                approved_count = approvedCount,
                paid_count = paidCount,
                archived_count = archivedCount,
                pre_payment_count = prePaymentCount,
                monthly_count = monthlyCount,
                quarterly_count = quarterlyCount,
                overdue_count = overdueCount,
                settlement_completion_rate = completionRate
            }
        });
    }

    [HttpPost("{id:int}/dispute")]
    public async Task<IActionResult> HandleDispute(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        var permissionResult = RequireRole(user, "finance_accountant", "admin");
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var disputeReason = ReadString(body, "dispute_reason", "disputeReason");
        var disputedItems = ReadRawJson(body, "disputed_items", "disputedItems");
        var supportingDocuments = ReadRawJson(body, "supporting_documents", "supportingDocuments");

        var settlement = await _dbContext.Settlements
            .FirstOrDefaultAsync(s => s.Id == id && (s.Type == "monthly" || s.Type == "quarterly"), cancellationToken);

        if (settlement == null)
        {
            return NotFound(new { message = "Settlement not found or dispute is not supported for this type." });
        }

        settlement.DisputeReceived = true;
        settlement.DisputeReason = disputeReason;
        settlement.DisputedItems = disputedItems;
        settlement.SupportingDocuments = supportingDocuments;
        settlement.DisputeProcessorId = TryParseUserId(user);
        settlement.DisputeReceivedAt = DateTimeOffset.UtcNow.ToString("o");
        settlement.Status = "pending_approval";

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(user, "settlement_dispute_handled", settlement.Id.ToString(), new
        {
            dispute_reason = disputeReason,
            disputed_items = disputedItems
        });

        return Ok(new { message = "Settlement dispute recorded." });
    }

    private async Task LogAuditAsync(AuthUser? actor, string action, string entityId, object payload)
    {
        if (actor == null)
        {
            return;
        }

        try
        {
            await _auditService.LogAsync(new AuditEntry
            {
                ActorId = actor.Id,
                ActorName = actor.Name,
                EntityType = "settlement",
                EntityId = entityId,
                Action = action,
                Changes = JsonSerializer.Serialize(payload),
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Settlements] Failed to write audit entry.");
        }
    }

    private static IActionResult? RequireRole(AuthUser? user, params string[] allowedRoles)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        var role = ControllerHelpers.NormalizeRole(user.Role);
        if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (allowedRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool TryReadIntFromQuery(IQueryCollection query, out int? value, params string[] keys)
    {
        value = null;
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var values))
            {
                if (int.TryParse(values.ToString(), out var parsed))
                {
                    value = parsed;
                    return true;
                }

                return false;
            }
        }

        return true;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
                {
                    return numeric;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
                {
                    return numeric;
                }
            }
        }

        return null;
    }

    private static List<int> ReadIntArray(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var results = new List<int>();
            foreach (var entry in value.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var numeric))
                {
                    results.Add(numeric);
                    continue;
                }

                if (entry.ValueKind == JsonValueKind.String && int.TryParse(entry.GetString(), out numeric))
                {
                    results.Add(numeric);
                }
            }

            return results;
        }

        return new List<int>();
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (body.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }

                if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
                {
                    return value.ToString();
                }
            }
        }

        return null;
    }

    private static bool? ReadBool(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric != 0;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var raw = value.GetString();
                if (bool.TryParse(raw, out var parsed))
                {
                    return parsed;
                }

                if (string.Equals(raw, "approved", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(raw, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (int.TryParse(raw, out numeric))
                {
                    return numeric != 0;
                }
            }
        }

        return null;
    }

    private static string? ReadRawJson(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            return value.GetRawText();
        }

        return null;
    }

    private static int? TryParseUserId(AuthUser? user)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.Id))
        {
            return null;
        }

        return int.TryParse(user.Id, out var parsed) ? parsed : null;
    }

    private static decimal GetSettlementAmount(Settlement settlement)
    {
        if (settlement.TotalAmount.HasValue)
        {
            return settlement.TotalAmount.Value;
        }

        if (settlement.GrandTotal.HasValue)
        {
            return settlement.GrandTotal.Value;
        }

        return 0m;
    }

    private async Task<decimal?> GetRfqAmountAsync(int? rfqId, CancellationToken cancellationToken)
    {
        if (!rfqId.HasValue)
        {
            return null;
        }

        return await _dbContext.Rfqs.AsNoTracking()
            .Where(r => r.Id == rfqId.Value)
            .Select(r => r.Amount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static decimal ParseTaxRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var trimmed = value.Trim();
        var hasPercent = trimmed.EndsWith("%", StringComparison.Ordinal);
        if (hasPercent)
        {
            trimmed = trimmed[..^1].Trim();
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) &&
            !decimal.TryParse(trimmed, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            return 0m;
        }

        if (hasPercent || parsed > 1m)
        {
            parsed /= 100m;
        }

        return parsed < 0 ? 0m : parsed;
    }
}
