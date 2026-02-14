using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Api.Models.Rfq;
using SupplierSystem.Api.StateMachines;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;
using DomainRfq = SupplierSystem.Domain.Entities.Rfq;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class RfqService(
    SupplierSystemDbContext dbContext,
    IAuditService auditService,
    ILogger<RfqService> logger,
    RfqStateMachine rfqStateMachine) : NodeServiceBase
{
    private readonly SupplierSystemDbContext _dbContext = dbContext;
    private readonly IAuditService _auditService = auditService;
    private readonly ILogger<RfqService> _logger = logger;
    private readonly RfqStateMachine _rfqStateMachine = rfqStateMachine;

    public Task<Dictionary<string, object?>> CreateAsync(CreateRfqRequest request, AuthUser user, CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });

        if (!string.IsNullOrWhiteSpace(request.ValidUntil) &&
            DateTime.TryParse(request.ValidUntil, out var deadline) &&
            deadline < DateTime.UtcNow)
        {
            throw new ValidationErrorException("Deadline cannot be in the past");
        }

        var now = DateTime.UtcNow.ToString("o");

        var rfq = new DomainRfq
        {
            Title = request.Title?.Trim(),
            Description = request.Description?.Trim(),
            RfqType = request.RfqType,
            DeliveryPeriod = request.DeliveryPeriod,
            BudgetAmount = request.BudgetAmount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CNY" : request.Currency,
            ValidUntil = request.ValidUntil,
            Status = "draft",
            CreatedBy = user.Id,
            CreatedAt = now,
            UpdatedAt = now,
            IsLineItemMode = true,
        };

        _dbContext.Rfqs.Add(rfq);
        return CreateRfqInternalAsync(rfq, request, now, user, cancellationToken);
    }

    public Task<(List<Dictionary<string, object?>> Data, int Total)> ListAsync(
        string? status,
        string? rfqType,
        string? keyword,
        int page,
        int pageSize,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Rfqs.AsNoTracking().AsQueryable();

        if (!HasAnyPermission(user, new[] { Permissions.RfqViewAll }))
        {
            query = query.Where(r => r.CreatedBy == user.Id);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(rfqType))
        {
            query = query.Where(r => r.RfqType == rfqType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r => (r.Title ?? string.Empty).Contains(keyword) ||
                                     (r.Description ?? string.Empty).Contains(keyword));
        }

        return ListInternalAsync(query, page, pageSize, cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetDetailsAsync(int id, AuthUser user, CancellationToken cancellationToken)
    {
        return GetDetailsInternalAsync(id, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> GetSupplierRfqAsync(int id, AuthUser user, CancellationToken cancellationToken)
    {
        return GetSupplierRfqInternalAsync(id, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> UpdateAsync(int id, CreateRfqRequest request, AuthUser user, CancellationToken cancellationToken)
    {
        return UpdateInternalAsync(id, request, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> PublishAsync(int id, AuthUser user, CancellationToken cancellationToken)
    {
        return PublishInternalAsync(id, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> CloseAsync(int id, AuthUser user, CancellationToken cancellationToken)
    {
        return CloseInternalAsync(id, user, cancellationToken);
    }

    public Task<bool> DeleteAsync(int id, AuthUser user, CancellationToken cancellationToken)
    {
        return DeleteInternalAsync(id, user, cancellationToken);
    }

    public Task<Dictionary<string, object?>> SendInvitationsAsync(int rfqId, List<int> supplierIds, AuthUser user, CancellationToken cancellationToken)
    {
        return SendInvitationsInternalAsync(rfqId, supplierIds, user, cancellationToken);
    }

    public Task<List<Dictionary<string, object?>>> GetSupplierInvitationsAsync(
        int supplierId,
        string? status,
        bool needsResponse,
        CancellationToken cancellationToken)
    {
        return GetSupplierInvitationsInternalAsync(supplierId, status, needsResponse, cancellationToken);
    }

    public Task<Dictionary<string, object?>> ReviewAsync(int id, ReviewRfqRequest request, AuthUser user, CancellationToken cancellationToken)
    {
        return ReviewInternalAsync(id, request, user, cancellationToken);
    }

    private async Task<Dictionary<string, object?>> CreateRfqInternalAsync(
        DomainRfq rfq,
        CreateRfqRequest request,
        string now,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (request.Items != null && request.Items.Count > 0)
        {
            var lineNumber = 1;
            foreach (var item in request.Items)
            {
                var lineItem = new RfqLineItem
                {
                    RfqId = rfq.Id,
                    LineNumber = item.LineNumber ?? lineNumber,
                    MaterialCategory = item.MaterialType,
                    ItemName = item.Description,
                    Quantity = item.Quantity ?? 0,
                    Unit = item.Unit,
                    EstimatedUnitPrice = item.TargetPrice,
                    Notes = item.Remarks,
                    Currency = rfq.Currency ?? "CNY",
                    CreatedAt = now,
                    Status = "draft",
                };

                _dbContext.RfqLineItems.Add(lineItem);
                lineNumber++;
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await _auditService.LogAsync(new Application.Models.Audit.AuditEntry
        {
            ActorId = user.Id,
            ActorName = user.Name,
            EntityType = "rfq",
            EntityId = rfq.Id.ToString(),
            Action = "create",
            Changes = JsonSerializer.Serialize(new { title = rfq.Title, status = rfq.Status }),
        }).ConfigureAwait(false);

        return await GetRfqWithItemsAsync(rfq.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(List<Dictionary<string, object?>> Data, int Total)> ListInternalAsync(
        IQueryable<DomainRfq> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var data = rows.Select(NodeCaseMapper.ToCamelCaseDictionary).ToList();
        return (data, total);
    }

    private async Task<Dictionary<string, object?>> GetDetailsInternalAsync(
        int id,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        var canView = rfq.CreatedBy == user.Id ||
                      HasAnyPermission(user, new[] { Permissions.RfqViewAll }) ||
                      (string.Equals(user.Role, "department_user", StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(user.Department) &&
                       string.Equals(rfq.RequestingDepartment, user.Department, StringComparison.OrdinalIgnoreCase));

        if (!canView)
        {
            throw new ServiceErrorException(403, "No permission to view this RFQ");
        }

        var result = await BuildRfqDetailsAsync(rfq, cancellationToken).ConfigureAwait(false);

        var visibility = await QuoteVisibility.GetVisibilityAsync(_dbContext, id, user, cancellationToken)
            .ConfigureAwait(false);

        if (visibility.Locked)
        {
            result["quotes"] = new List<object>();
        }

        if (string.Equals(user.Role, "department_user", StringComparison.OrdinalIgnoreCase) && !visibility.Locked)
        {
            if (result.TryGetValue("selectedQuoteId", out var selectedObj) &&
                selectedObj is int selectedQuoteId &&
                result.TryGetValue("quotes", out var quotesObj) &&
                quotesObj is List<Dictionary<string, object?>> quoteList)
            {
                result["quotes"] = quoteList.Where(q => q.TryGetValue("id", out var idValue) &&
                                                        idValue is int quoteId &&
                                                        quoteId == selectedQuoteId)
                                            .ToList();
            }
        }

        result["quotesVisible"] = !visibility.Locked;
        result["visibilityReason"] = visibility.Locked
            ? new
            {
                totalInvited = visibility.Context.InvitedCount,
                submittedCount = visibility.Context.SubmittedCount,
                deadline = visibility.Context.Deadline,
                message = QuoteVisibility.QuoteVisibilityLockMessage,
            }
            : null;

        return result;
    }

    private async Task<Dictionary<string, object?>> GetSupplierRfqInternalAsync(
        int id,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        if (!user.SupplierId.HasValue)
        {
            throw new ServiceErrorException(403, "Only suppliers can access this RFQ");
        }

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        var invitation = await _dbContext.SupplierRfqInvitations.AsNoTracking()
            .FirstOrDefaultAsync(i => i.RfqId == id && i.SupplierId == user.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
        {
            throw new ServiceErrorException(403, "No invitation found for this supplier");
        }

        var result = await BuildRfqDetailsAsync(rfq, cancellationToken).ConfigureAwait(false);

        var supplierQuotes = new List<Dictionary<string, object?>>();
        if (result.TryGetValue("quotes", out var quotesObj) && quotesObj is List<Dictionary<string, object?>> quotes)
        {
            supplierQuotes = quotes
                .Where(q => q.TryGetValue("supplierId", out var supplierIdObj) &&
                            supplierIdObj is int supplierId &&
                            supplierId == user.SupplierId)
                .ToList();
        }

        Dictionary<string, object?>? supplierQuote = supplierQuotes.FirstOrDefault();

        int? daysRemaining = null;
        if (!string.IsNullOrWhiteSpace(rfq.ValidUntil) &&
            DateTime.TryParse(rfq.ValidUntil, out var deadline))
        {
            var diffTime = deadline - DateTime.UtcNow;
            daysRemaining = (int)Math.Ceiling(diffTime.TotalDays);
        }

        var quoteStatus = supplierQuote != null && supplierQuote.TryGetValue("status", out var statusObj)
            ? statusObj?.ToString() ?? "submitted"
            : "not_submitted";

        var needsResponse = supplierQuote == null && string.Equals(rfq.Status, "published", StringComparison.OrdinalIgnoreCase);

        var invitationDict = NodeCaseMapper.ToCamelCaseDictionary(invitation);
        invitationDict["rfqStatus"] = rfq.Status;
        invitationDict["quoteStatus"] = quoteStatus;
        invitationDict["validUntil"] = rfq.ValidUntil;
        invitationDict["daysRemaining"] = daysRemaining;
        invitationDict["needsResponse"] = needsResponse;

        result["quotes"] = supplierQuotes;
        result["supplierInvitation"] = invitationDict;
        result["invitation"] = invitationDict;
        result.Remove("invitations");

        return result;
    }

    private async Task<Dictionary<string, object?>> UpdateInternalAsync(
        int id,
        CreateRfqRequest request,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (existing == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        var canEdit = existing.CreatedBy == user.Id ||
                      HasAnyPermission(user, new[] { Permissions.RfqEditAll });

        if (!canEdit)
        {
            throw new ServiceErrorException(403, "No permission to edit this RFQ");
        }

        if (!string.Equals(existing.Status, "draft", StringComparison.OrdinalIgnoreCase) &&
            !HasAnyPermission(user, new[] { Permissions.RfqEditAll }))
        {
            throw new ServiceErrorException(400, "Cannot edit published RFQ");
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            existing.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            existing.Description = request.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.DeliveryPeriod))
        {
            existing.DeliveryPeriod = request.DeliveryPeriod;
        }

        if (request.BudgetAmount.HasValue)
        {
            existing.BudgetAmount = request.BudgetAmount;
        }

        if (!string.IsNullOrWhiteSpace(request.ValidUntil))
        {
            existing.ValidUntil = request.ValidUntil;
        }

        existing.UpdatedAt = DateTime.UtcNow.ToString("o");

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NodeCaseMapper.ToCamelCaseDictionary(existing);
    }

    private async Task<Dictionary<string, object?>> PublishInternalAsync(
        int id,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqPublish });

        var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        var now = DateTime.UtcNow.ToString("o");
        var updated = await _rfqStateMachine.TransitionAsync(
                rfq,
                RfqStateMachine.Statuses.Published,
                user,
                "Manual publish",
                async (entity, status, token) =>
                {
                    entity.Status = status;
                    entity.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync(token).ConfigureAwait(false);
                    return entity;
                },
                cancellationToken)
            .ConfigureAwait(false);

        return NodeCaseMapper.ToCamelCaseDictionary(updated);
    }

    private async Task<Dictionary<string, object?>> CloseInternalAsync(
        int id,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqClose });

        var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        var now = DateTime.UtcNow.ToString("o");
        var updated = await _rfqStateMachine.TransitionAsync(
                rfq,
                RfqStateMachine.Statuses.Closed,
                user,
                "Manual close",
                async (entity, status, token) =>
                {
                    entity.Status = status;
                    entity.UpdatedAt = now;
                    await _dbContext.SaveChangesAsync(token).ConfigureAwait(false);
                    return entity;
                },
                cancellationToken)
            .ConfigureAwait(false);

        return NodeCaseMapper.ToCamelCaseDictionary(updated);
    }

    private async Task<bool> DeleteInternalAsync(int id, AuthUser user, CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqDelete });

        var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        if (!string.Equals(rfq.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Only draft RFQ can be deleted");
        }

        _dbContext.Rfqs.Remove(rfq);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<Dictionary<string, object?>> SendInvitationsInternalAsync(
        int rfqId,
        List<int> supplierIds,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqInviteSuppliers });

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {rfqId} not found");
        }

        if (!string.Equals(rfq.Status, "published", StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Can only invite suppliers to published RFQ");
        }

        if (supplierIds.Count == 0)
        {
            throw new ValidationErrorException("At least one supplier ID is required");
        }

        var now = DateTime.UtcNow.ToString("o");
        var invitations = new List<Dictionary<string, object?>>();

        foreach (var supplierId in supplierIds)
        {
            var invitation = new SupplierRfqInvitation
            {
                RfqId = rfqId,
                SupplierId = supplierId,
                Status = "sent",
                InvitedAt = now,
            };
            _dbContext.SupplierRfqInvitations.Add(invitation);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            invitations.Add(new Dictionary<string, object?>
            {
                ["id"] = invitation.Id,
                ["rfqId"] = rfqId,
                ["supplierId"] = supplierId,
                ["status"] = "sent",
                ["sentAt"] = now,
            });
        }

        return new Dictionary<string, object?>
        {
            ["success"] = true,
            ["count"] = invitations.Count,
            ["invitations"] = invitations,
        };
    }

    private async Task<List<Dictionary<string, object?>>> GetSupplierInvitationsInternalAsync(
        int supplierId,
        string? status,
        bool needsResponse,
        CancellationToken cancellationToken)
    {
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
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var rfqDict = NodeCaseMapper.ToCamelCaseDictionary(row.rfq);
            rfqDict["invitationStatus"] = row.inv.Status;
            rfqDict["invitationSentAt"] = row.inv.InvitedAt;
            rfqDict["quoteId"] = row.quoteId;
            rfqDict["quoteStatus"] = row.quoteStatus;
            results.Add(rfqDict);
        }

        return results;
    }

    private async Task<Dictionary<string, object?>> ReviewInternalAsync(
        int id,
        ReviewRfqRequest request,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequireAnyPermission(user, new[]
        {
            Permissions.PurchaserRfqTarget,
            Permissions.ProcurementManagerRfqReview,
            Permissions.ProcurementDirectorRfqApprove,
        });

        if (!request.SelectedQuoteId.HasValue || string.IsNullOrWhiteSpace(request.ReviewScoresJson))
        {
            throw new ValidationErrorException("Missing required fields: selectedQuoteId, reviewScores");
        }

        var rfq = await _dbContext.Rfqs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        if (rfq.Status != "in_progress" && rfq.Status != "published")
        {
            throw new ServiceErrorException(400, "RFQ is not in a state that allows review");
        }

        var quote = await _dbContext.Quotes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == request.SelectedQuoteId && q.RfqId == id, cancellationToken)
            .ConfigureAwait(false);

        if (quote == null)
        {
            throw new ServiceErrorException(404, "Selected quote not found for this RFQ");
        }

        var now = DateTime.UtcNow.ToString("o");
        var reason = $"RFQ reviewed and closed by {user.Name}";
        var updated = await _rfqStateMachine.TransitionAsync(
                rfq,
                RfqStateMachine.Statuses.Closed,
                user,
                reason,
                async (entity, status, token) =>
                {
                    entity.Status = status;
                    entity.UpdatedAt = now;

                    if (quote.Status != "selected")
                    {
                        var quoteEntity = await _dbContext.Quotes.FirstOrDefaultAsync(q => q.Id == quote.Id, token)
                            .ConfigureAwait(false);
                        if (quoteEntity != null)
                        {
                            quoteEntity.Status = "selected";
                            quoteEntity.UpdatedAt = now;
                        }
                    }

                    _dbContext.RfqReviews.Add(new RfqReview
                    {
                        RfqId = id,
                        SelectedQuoteId = request.SelectedQuoteId,
                        ReviewScores = request.ReviewScoresJson,
                        Comments = request.Comments,
                        ReviewedBy = user.Name,
                        ReviewedAt = now,
                    });

                    await _dbContext.SaveChangesAsync(token).ConfigureAwait(false);
                    return entity;
                },
                cancellationToken)
            .ConfigureAwait(false);

        return NodeCaseMapper.ToCamelCaseDictionary(updated);
    }

    private Task<Dictionary<string, object?>> GetRfqWithItemsAsync(long id, CancellationToken cancellationToken)
    {
        return GetRfqWithItemsInternalAsync(id, cancellationToken);
    }

    private Task<Dictionary<string, object?>> BuildRfqDetailsAsync(DomainRfq rfq, CancellationToken cancellationToken)
    {
        return BuildRfqDetailsInternalAsync(rfq, cancellationToken);
    }

    private static Dictionary<string, object?> MapClassicItem(RfqLineItem item)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = item.Id,
            ["rfqId"] = item.RfqId,
            ["lineNumber"] = item.LineNumber,
            ["materialType"] = item.MaterialCategory,
            ["description"] = item.ItemName,
            ["quantity"] = item.Quantity,
            ["unit"] = item.Unit,
            ["targetPrice"] = item.EstimatedUnitPrice,
            ["remarks"] = item.Notes,
            ["createdAt"] = item.CreatedAt,
        };
    }

    private async Task<Dictionary<string, object?>> GetRfqWithItemsInternalAsync(long id, CancellationToken cancellationToken)
    {
        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (rfq == null)
        {
            throw new Exception($"RFQ with id {id} not found");
        }

        var rfqDict = NodeCaseMapper.ToCamelCaseDictionary(rfq);
        var lineItems = await _dbContext.RfqLineItems.AsNoTracking()
            .Where(li => li.RfqId == id)
            .OrderBy(li => li.LineNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        rfqDict["items"] = lineItems.Select(MapClassicItem).ToList();
        return rfqDict;
    }

    private async Task<Dictionary<string, object?>> BuildRfqDetailsInternalAsync(DomainRfq rfq, CancellationToken cancellationToken)
    {
        var rfqDict = NodeCaseMapper.ToCamelCaseDictionary(rfq);

        var lineItems = await _dbContext.RfqLineItems.AsNoTracking()
            .Where(li => li.RfqId == rfq.Id)
            .OrderBy(li => li.LineNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        rfqDict["items"] = lineItems.Select(MapClassicItem).ToList();

        var quotes = await (from q in _dbContext.Quotes.AsNoTracking()
                            join s in _dbContext.Suppliers.AsNoTracking() on q.SupplierId equals s.Id into supplierGroup
                            from s in supplierGroup.DefaultIfEmpty()
                            where q.RfqId == rfq.Id && q.IsLatest
                            orderby q.SubmittedAt descending
                            select new { q, supplierName = s != null ? s.CompanyName : null })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var quoteIds = quotes.Select(q => q.q.Id).ToList();
        var quoteLineItems = await _dbContext.QuoteLineItems.AsNoTracking()
            .Where(qli => quoteIds.Contains(qli.QuoteId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var quoteItemsLookup = quoteLineItems
            .GroupBy(qli => qli.QuoteId)
            .ToDictionary(g => g.Key, g => NodeCaseMapper.ToCamelCaseList(g));

        var attachmentLookup = new Dictionary<long, List<Dictionary<string, object?>>>();
        if (quoteIds.Count > 0)
        {
            var attachments = await _dbContext.QuoteAttachments.AsNoTracking()
                .Where(att => quoteIds.Contains(att.QuoteId))
                .OrderByDescending(att => att.UploadedAt)
                .ThenByDescending(att => att.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            attachmentLookup = attachments
                .GroupBy(att => att.QuoteId)
                .ToDictionary(g => g.Key, g => NodeCaseMapper.ToCamelCaseList(g));

            foreach (var attachmentGroup in attachmentLookup.Values)
            {
                foreach (var attachment in attachmentGroup)
                {
                    if (attachment.TryGetValue("storedName", out var storedObj) &&
                        storedObj is string storedName &&
                        !string.IsNullOrWhiteSpace(storedName))
                    {
                        var downloadUrl = $"/uploads/rfq-attachments/{rfq.Id.ToString(CultureInfo.InvariantCulture)}/{storedName}";
                        attachment["filePath"] = downloadUrl;
                        attachment["downloadUrl"] = downloadUrl;
                    }
                }
            }
        }

        var quoteList = new List<Dictionary<string, object?>>();
        foreach (var entry in quotes)
        {
            var quoteDict = NodeCaseMapper.ToCamelCaseDictionary(entry.q);
            quoteDict["supplierName"] = entry.supplierName;
            quoteDict["quoteItems"] = quoteItemsLookup.TryGetValue(entry.q.Id, out var items)
                ? items
                : new List<Dictionary<string, object?>>();
            quoteDict["attachments"] = attachmentLookup.TryGetValue(entry.q.Id, out var files)
                ? files
                : new List<Dictionary<string, object?>>();
            quoteList.Add(quoteDict);
        }

        rfqDict["quotes"] = quoteList;

        var invitations = await (from inv in _dbContext.SupplierRfqInvitations.AsNoTracking()
                                 join s in _dbContext.Suppliers.AsNoTracking()
                                     on inv.SupplierId equals s.Id into supplierGroup
                                 from s in supplierGroup.DefaultIfEmpty()
                                 where inv.RfqId == rfq.Id
                                 orderby inv.InvitedAt descending
                                 select new
                                 {
                                     inv,
                                     supplierName = s != null ? s.CompanyName : null,
                                     supplierEmail = s != null ? s.ContactEmail : null,
                                     companyId = s != null ? s.CompanyId : null,
                                     supplierCode = s != null ? s.SupplierCode : null,
                                     vendorCode = s != null ? (s.SupplierCode ?? s.CompanyId) : null,
                                 })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var invitationList = new List<Dictionary<string, object?>>();
        foreach (var entry in invitations)
        {
            var invitationDict = NodeCaseMapper.ToCamelCaseDictionary(entry.inv);
            invitationDict["invitationStatus"] = entry.inv.Status;
            invitationDict["supplierName"] = entry.supplierName;
            invitationDict["supplierEmail"] = entry.supplierEmail;
            invitationDict["companyId"] = entry.companyId;
            invitationDict["supplierCode"] = entry.supplierCode;
            invitationDict["vendorCode"] = entry.vendorCode;
            invitationList.Add(invitationDict);
        }

        rfqDict["invitations"] = invitationList;

        return rfqDict;
    }

    private static void ValidateRfqTransition(string? fromStatus, string toStatus)
    {
        var transitions = new Dictionary<string, string[]>
        {
            ["draft"] = new[] { "published", "cancelled" },
            ["published"] = new[] { "in_progress", "closed", "cancelled" },
            ["in_progress"] = new[] { "confirmed", "closed", "cancelled" },
            ["closed"] = Array.Empty<string>(),
            ["cancelled"] = Array.Empty<string>(),
            ["confirmed"] = new[] { "closed" },
        };

        var statusLabels = new Dictionary<string, string>
        {
            ["draft"] = "草稿",
            ["published"] = "已发布",
            ["in_progress"] = "进行中",
            ["closed"] = "已关闭",
            ["cancelled"] = "已取消",
            ["confirmed"] = "已确认",
        };

        string[]? allowed = null;
        if (!string.IsNullOrWhiteSpace(fromStatus))
        {
            transitions.TryGetValue(fromStatus, out allowed);
        }

        if (string.IsNullOrWhiteSpace(fromStatus) ||
            allowed == null ||
            !allowed.Contains(toStatus))
        {
            var fromLabel = fromStatus != null && statusLabels.TryGetValue(fromStatus, out var labelFrom)
                ? labelFrom
                : fromStatus;
            var toLabel = statusLabels.TryGetValue(toStatus, out var labelTo)
                ? labelTo
                : toStatus;
            var allowedLabel = allowed == null
                ? string.Empty
                : string.Join(", ", allowed.Select(status => statusLabels.TryGetValue(status, out var label) ? label : status));
            var allowedText = string.IsNullOrWhiteSpace(allowedLabel) ? "none" : allowedLabel;

            throw new Exception(
                $"Invalid state transition: Cannot change from \"{fromLabel}\" to \"{toLabel}\". " +
                $"Allowed transitions: {allowedText}");
        }
    }

    private Task RecordRfqStatusHistoryAsync(
        int rfqId,
        string? fromStatus,
        string toStatus,
        AuthUser user,
        string? reason,
        CancellationToken cancellationToken)
    {
        return RecordRfqStatusHistoryInternalAsync(rfqId, fromStatus, toStatus, user, reason, cancellationToken);
    }

    private async Task RecordRfqStatusHistoryInternalAsync(
        int rfqId,
        string? fromStatus,
        string toStatus,
        AuthUser user,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.RfqStatusHistories.Add(new RfqStatusHistory
            {
                RfqId = rfqId,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                ChangedBy = user.Id,
                ChangedAt = DateTime.UtcNow.ToString("o"),
                Reason = reason,
            });

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record RFQ status history for RFQ {RfqId}", rfqId);
        }
    }
}

