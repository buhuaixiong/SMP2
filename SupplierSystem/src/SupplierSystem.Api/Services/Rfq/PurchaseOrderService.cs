using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Api.Helpers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Rfq;

public sealed class PurchaseOrderService(
    SupplierSystemDbContext dbContext,
    IAuditService auditService,
    ILogger<PurchaseOrderService> logger) : NodeServiceBase
{
    private readonly SupplierSystemDbContext _dbContext = dbContext;
    private readonly IAuditService _auditService = auditService;
    private readonly ILogger<PurchaseOrderService> _logger = logger;

    private static class PoStatus
    {
        public const string Draft = "draft";
        public const string Submitted = "submitted";
        public const string Confirmed = "confirmed";
    }

    public async Task<Dictionary<string, object?>> CreatePoAsync(
        int rfqId,
        int supplierId,
        List<int> lineItemIds,
        string? description,
        string? notes,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });
        ValidateRequired(new Dictionary<string, object?>
        {
            ["rfqId"] = rfqId,
            ["supplierId"] = supplierId,
        }, new[] { "rfqId", "supplierId" });

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq == null)
        {
            throw new Exception($"RFQ with id {rfqId} not found");
        }

        if (!string.Equals(rfq.CreatedBy, user.Id, StringComparison.Ordinal))
        {
            throw new ServiceErrorException(403, "Only RFQ creator can create PO");
        }

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);
        if (supplier == null)
        {
            throw new Exception($"Supplier with id {supplierId} not found");
        }

        if (lineItemIds.Count == 0)
        {
            throw new ValidationErrorException("At least one line item is required");
        }

        var totalAmount = 0m;
        var lineItems = new List<RfqLineItem>();

        foreach (var lineItemId in lineItemIds)
        {
            var record = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                                join q in _dbContext.Quotes.AsNoTracking()
                                    on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                                from q in quoteGroup.DefaultIfEmpty()
                                where li.Id == lineItemId && li.RfqId == rfqId
                                select new { LineItem = li, Quote = q })
                .FirstOrDefaultAsync(cancellationToken);

            if (record == null)
            {
                throw new Exception($"Line item with id {lineItemId} not found");
            }

            if (!string.Equals(record.LineItem.Status, "pending_po", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServiceErrorException(400, $"Line item {lineItemId} is not ready for PO (status: {record.LineItem.Status})");
            }

            if (record.Quote == null || record.Quote.SupplierId != supplierId)
            {
                throw new ServiceErrorException(400, $"Line item {lineItemId} belongs to a different supplier");
            }

            lineItems.Add(record.LineItem);
            totalAmount += record.Quote.TotalAmount ?? 0m;
        }

        var now = DateTime.UtcNow.ToString("o");
        var poNumber = GeneratePoNumber();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var po = new PurchaseOrder
        {
            PoNumber = poNumber,
            RfqId = rfqId,
            SupplierId = supplierId,
            TotalAmount = totalAmount,
            Currency = "CNY",
            ItemCount = lineItems.Count,
            Status = PoStatus.Draft,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            CreatedBy = user.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _dbContext.PurchaseOrders.Add(po);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await LogAuditAsync("purchase_order", po.Id.ToString(CultureInfo.InvariantCulture), "create",
            new { rfqId, supplierId, lineItemCount = lineItems.Count, totalAmount }, user);

        return NodeCaseMapper.ToSnakeCaseDictionary(po);
    }

    public async Task<Dictionary<string, object?>> UpdatePoAsync(
        int poId,
        Dictionary<string, object?> updateData,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });

        var po = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);
        if (po == null)
        {
            throw new Exception($"Purchase Order with id {poId} not found");
        }

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == po.RfqId, cancellationToken);
        if (rfq != null && !string.Equals(rfq.CreatedBy, user.Id, StringComparison.Ordinal))
        {
            throw new ServiceErrorException(403, "Only RFQ creator can update PO");
        }

        if (!string.Equals(po.Status, PoStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Only draft POs can be updated");
        }

        var changed = false;

        if (updateData.TryGetValue("description", out var description))
        {
            po.Description = description?.ToString();
            changed = true;
        }

        if (updateData.TryGetValue("notes", out var notes))
        {
            po.Notes = notes?.ToString();
            changed = true;
        }

        if (updateData.TryGetValue("po_file_path", out var filePath))
        {
            po.PoFilePath = filePath?.ToString();
            changed = true;
        }

        if (updateData.TryGetValue("po_file_name", out var fileName))
        {
            po.PoFileName = fileName?.ToString();
            changed = true;
        }

        if (updateData.TryGetValue("po_file_size", out var fileSize))
        {
            po.PoFileSize = fileSize is long sizeValue ? sizeValue :
                long.TryParse(fileSize?.ToString(), out var parsed) ? parsed : po.PoFileSize;
            changed = true;
        }

        if (!changed)
        {
            return NodeCaseMapper.ToSnakeCaseDictionary(po);
        }

        po.UpdatedAt = DateTime.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync("purchase_order", po.Id.ToString(CultureInfo.InvariantCulture), "update",
            updateData, user);

        return NodeCaseMapper.ToSnakeCaseDictionary(po);
    }

    public async Task<Dictionary<string, object?>> SubmitPoAsync(
        int poId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });

        var po = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);
        if (po == null)
        {
            throw new Exception($"Purchase Order with id {poId} not found");
        }

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == po.RfqId, cancellationToken);
        if (rfq != null && !string.Equals(rfq.CreatedBy, user.Id, StringComparison.Ordinal))
        {
            throw new ServiceErrorException(403, "Only RFQ creator can submit PO");
        }

        if (!string.Equals(po.Status, PoStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Only draft POs can be submitted");
        }

        if (string.IsNullOrWhiteSpace(po.PoFilePath))
        {
            throw new ValidationErrorException("PO file is required before submission");
        }

        po.Status = PoStatus.Submitted;
        po.UpdatedAt = DateTime.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync("purchase_order", po.Id.ToString(CultureInfo.InvariantCulture), "submit",
            new { status = PoStatus.Submitted }, user);

        return NodeCaseMapper.ToSnakeCaseDictionary(po);
    }

    public async Task<Dictionary<string, object?>> GetPoAsync(
        int poId,
        CancellationToken cancellationToken)
    {
        var po = await _dbContext.PurchaseOrders.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);
        if (po == null)
        {
            throw new Exception($"Purchase Order with id {poId} not found");
        }

        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == po.SupplierId, cancellationToken);

        var poDict = NodeCaseMapper.ToSnakeCaseDictionary(po);
        if (supplier != null)
        {
            poDict["supplier_name"] = supplier.CompanyName;
            poDict["supplier_contact"] = supplier.ContactPerson;
            poDict["supplier_phone"] = supplier.ContactPhone;
            poDict["supplier_email"] = supplier.ContactEmail;
        }

        var lineItemRecords = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                                     join q in _dbContext.Quotes.AsNoTracking()
                                         on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                                     from q in quoteGroup.DefaultIfEmpty()
                                     where li.PoId == poId
                                     orderby li.LineNumber
                                     select new
                                     {
                                         li.Id,
                                         li.RfqId,
                                         li.LineNumber,
                                         li.MaterialCategory,
                                         li.Brand,
                                         li.ItemName,
                                         li.Specifications,
                                         li.Quantity,
                                         li.Unit,
                                         li.EstimatedUnitPrice,
                                         li.Currency,
                                         li.Parameters,
                                         li.Notes,
                                         li.CreatedAt,
                                         li.Status,
                                         li.CurrentApproverRole,
                                         li.SelectedQuoteId,
                                         li.PoId,
                                         li.UpdatedAt,
                                         QuoteAmount = q != null ? (decimal?)q.TotalAmount : null,
                                         QuoteCurrency = q != null ? q.Currency : null,
                                     })
            .ToListAsync(cancellationToken);

        var lineItems = lineItemRecords.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();

        poDict["lineItems"] = lineItems;

        return poDict;
    }

    public async Task<List<Dictionary<string, object?>>> ListPosForRfqAsync(
        int rfqId,
        CancellationToken cancellationToken)
    {
        var recordItems = await (from po in _dbContext.PurchaseOrders.AsNoTracking()
                                 join s in _dbContext.Suppliers.AsNoTracking()
                                     on po.SupplierId equals s.Id into supplierGroup
                                 from s in supplierGroup.DefaultIfEmpty()
                                 where po.RfqId == rfqId
                                 orderby po.CreatedAt descending
                                 select new
                                 {
                                     po.Id,
                                     po.PoNumber,
                                     po.RfqId,
                                     po.SupplierId,
                                     po.TotalAmount,
                                     po.Currency,
                                     po.ItemCount,
                                     po.PoFilePath,
                                     po.PoFileName,
                                     po.PoFileSize,
                                     po.Status,
                                     po.Description,
                                     po.Notes,
                                     po.CreatedBy,
                                     po.CreatedAt,
                                     po.UpdatedAt,
                                     SupplierName = s != null ? s.CompanyName : null,
                                 })
            .ToListAsync(cancellationToken);

        return recordItems.Select(NodeCaseMapper.ToSnakeCaseDictionary).ToList();
    }

    public async Task<List<Dictionary<string, object?>>> GetAvailableLineItemsGroupedBySupplierAsync(
        int rfqId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rfqId, cancellationToken);
        if (rfq == null)
        {
            throw new Exception($"RFQ with id {rfqId} not found");
        }

        if (!string.Equals(rfq.CreatedBy, user.Id, StringComparison.Ordinal))
        {
            throw new ServiceErrorException(403, "Only RFQ creator can view line items");
        }

        var lineItems = await (from li in _dbContext.RfqLineItems.AsNoTracking()
                               join q in _dbContext.Quotes.AsNoTracking()
                                   on li.SelectedQuoteId equals (long?)q.Id into quoteGroup
                               from q in quoteGroup.DefaultIfEmpty()
                               join s in _dbContext.Suppliers.AsNoTracking()
                                   on q.SupplierId equals s.Id into supplierGroup
                               from s in supplierGroup.DefaultIfEmpty()
                               where li.RfqId == rfqId && li.Status == "pending_po"
                               orderby q.SupplierId, li.LineNumber
                               select new
                               {
                                   LineItem = li,
                                   Quote = q,
                                   Supplier = s
                               })
            .ToListAsync(cancellationToken);

        const int UnknownSupplierKey = -1;
        var grouped = new Dictionary<int, Dictionary<string, object?>>();

        foreach (var entry in lineItems)
        {
            var supplierId = entry.Quote?.SupplierId;
            var key = supplierId ?? UnknownSupplierKey;
            if (!grouped.TryGetValue(key, out var group))
            {
                group = new Dictionary<string, object?>
                {
                    ["supplierId"] = supplierId,
                    ["supplierName"] = entry.Supplier?.CompanyName,
                    ["lineItems"] = new List<Dictionary<string, object?>>(),
                    ["totalAmount"] = 0m,
                };
                grouped[key] = group;
            }

            var list = (List<Dictionary<string, object?>>)group["lineItems"]!;
            list.Add(new Dictionary<string, object?>
            {
                ["id"] = entry.LineItem.Id,
                ["rfq_id"] = entry.LineItem.RfqId,
                ["line_number"] = entry.LineItem.LineNumber,
                ["material_category"] = entry.LineItem.MaterialCategory,
                ["brand"] = entry.LineItem.Brand,
                ["item_name"] = entry.LineItem.ItemName,
                ["specifications"] = entry.LineItem.Specifications,
                ["quantity"] = entry.LineItem.Quantity,
                ["unit"] = entry.LineItem.Unit,
                ["estimated_unit_price"] = entry.LineItem.EstimatedUnitPrice,
                ["currency"] = entry.LineItem.Currency,
                ["parameters"] = entry.LineItem.Parameters,
                ["notes"] = entry.LineItem.Notes,
                ["created_at"] = entry.LineItem.CreatedAt,
                ["status"] = entry.LineItem.Status,
                ["current_approver_role"] = entry.LineItem.CurrentApproverRole,
                ["selected_quote_id"] = entry.LineItem.SelectedQuoteId,
                ["po_id"] = entry.LineItem.PoId,
                ["updated_at"] = entry.LineItem.UpdatedAt,
                ["supplier_id"] = entry.Quote?.SupplierId,
                ["quote_amount"] = entry.Quote?.TotalAmount,
                ["quote_currency"] = entry.Quote?.Currency,
                ["supplier_name"] = entry.Supplier?.CompanyName,
            });

            var amount = entry.Quote?.TotalAmount ?? 0m;
            group["totalAmount"] = (decimal)group["totalAmount"]! + amount;
        }

        return grouped.Values.ToList();
    }

    public async Task DeletePoAsync(
        int poId,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        RequirePermissions(user, new[] { Permissions.RfqCreate });

        var po = await _dbContext.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == poId, cancellationToken);
        if (po == null)
        {
            throw new Exception($"Purchase Order with id {poId} not found");
        }

        var rfq = await _dbContext.Rfqs.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == po.RfqId, cancellationToken);
        if (rfq != null && !string.Equals(rfq.CreatedBy, user.Id, StringComparison.Ordinal))
        {
            throw new ServiceErrorException(403, "Only RFQ creator can delete PO");
        }

        if (!string.Equals(po.Status, PoStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ServiceErrorException(400, "Only draft POs can be deleted");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var items = await _dbContext.RfqLineItems
            .Where(li => li.PoId == poId)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.PoId = null;
        }

        _dbContext.PurchaseOrders.Remove(po);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await LogAuditAsync("purchase_order", poId.ToString(CultureInfo.InvariantCulture), "delete",
            new { poId }, user);
    }

    private static string GeneratePoNumber()
    {
        var prefix = "PO";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = Random.Shared.Next(0, 1000).ToString("D3", CultureInfo.InvariantCulture);
        return $"{prefix}{timestamp}{random}";
    }

    private async Task LogAuditAsync(
        string entityType,
        string entityId,
        string action,
        object? changes,
        AuthUser user)
    {
        try
        {
            var entry = new SupplierSystem.Application.Models.Audit.AuditEntry
            {
                ActorId = user.Id,
                ActorName = user.Name,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Changes = changes == null ? null : JsonSerializer.Serialize(changes),
            };

            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit entry for {EntityType} {EntityId}", entityType, entityId);
        }
    }
}
