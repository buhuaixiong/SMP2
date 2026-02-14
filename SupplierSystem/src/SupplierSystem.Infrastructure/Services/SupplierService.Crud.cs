using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 基础CRUD

    public async Task<SupplierResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier == null)
            return null;

        return await EnrichSupplierAsync(supplier, true, cancellationToken);
    }

    public async Task<SupplierListResponse> ListSuppliersAsync(
        SupplierListQuery query,
        AuthUser? user,
        CancellationToken cancellationToken)
    {
        var baseQuery = _context.Suppliers.AsNoTracking().AsQueryable();

        if (ShouldRestrictToAssignedSuppliers(user, query.ForRfq))
        {
            var buyerId = user!.Id;
            var assignedSupplierIds = _context.BuyerSupplierAssignments
                .AsNoTracking()
                .Where(a => a.BuyerId == buyerId)
                .Select(a => a.SupplierId);
            baseQuery = baseQuery.Where(s => assignedSupplierIds.Contains(s.Id));
        }

        // 应用过滤条件
        if (!string.IsNullOrEmpty(query.Status))
            baseQuery = baseQuery.Where(s => s.Status == query.Status);

        if (!string.IsNullOrEmpty(query.Category))
            baseQuery = baseQuery.Where(s => s.Category == query.Category);

        if (!string.IsNullOrEmpty(query.Region))
            baseQuery = baseQuery.Where(s => s.Region == query.Region);

        if (!string.IsNullOrEmpty(query.Stage))
            baseQuery = baseQuery.Where(s => s.Stage == query.Stage);

        if (!string.IsNullOrEmpty(query.Importance))
            baseQuery = baseQuery.Where(s => s.Importance == query.Importance);

        if (!string.IsNullOrEmpty(query.Query))
        {
            var trimmed = query.Query.Trim();
            var searchTerm = $"%{trimmed}%";
            var parsedId = int.TryParse(trimmed, out var supplierId) ? supplierId : (int?)null;
            baseQuery = baseQuery.Where(s =>
                EF.Functions.Like(s.CompanyName, searchTerm) ||
                EF.Functions.Like(s.CompanyId, searchTerm) ||
                EF.Functions.Like(s.ContactPerson, searchTerm) ||
                EF.Functions.Like(s.ContactEmail, searchTerm) ||
                EF.Functions.Like(s.SupplierCode ?? string.Empty, searchTerm) ||
                (parsedId.HasValue && s.Id == parsedId.Value));
        }

        // 标签过滤
        if (!string.IsNullOrEmpty(query.Tag))
        {
            baseQuery = baseQuery
                .Join(_context.SupplierTags, s => s.Id, st => st.SupplierId, (s, st) => new { s, st })
                .Join(_context.TagDefs, x => x.st.TagId, t => t.Id, (x, t) => new { x.s, t.Name })
                .Where(x => EF.Functions.Like(x.Name, $"%{query.Tag}%"))
                .Select(x => x.s);
        }

        var total = await baseQuery.CountAsync(cancellationToken);

        var suppliers = await baseQuery
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Skip(query.Offset ?? 0)
            .Take(query.Limit ?? 50)
            .ToListAsync(cancellationToken);

        if (suppliers.Count == 0)
        {
            return new SupplierListResponse
            {
                Total = total,
                Suppliers = new List<SupplierResponse>()
            };
        }

        var supplierIds = suppliers.Select(s => s.Id).ToList();
        var tagsLookup = await GetSupplierTagsLookupAsync(supplierIds, cancellationToken);
        var completionLookup = await GetLatestCompletionHistoryLookupAsync(supplierIds, cancellationToken);

        var list = new List<SupplierResponse>(suppliers.Count);
        foreach (var supplier in suppliers)
        {
            var response = MapToResponse(supplier);
            response.Tags = tagsLookup.TryGetValue(supplier.Id, out var tags)
                ? tags
                : new List<TagResponse>();
            response.ComplianceSummary = BuildComplianceSummaryFromHistory(
                supplier,
                completionLookup.TryGetValue(supplier.Id, out var history) ? history : null);
            response.MissingRequirements = response.ComplianceSummary.MissingItems;
            list.Add(response);
        }

        return new SupplierListResponse
        {
            Total = total,
            Suppliers = list
        };
    }

    private static bool ShouldRestrictToAssignedSuppliers(AuthUser? user, bool forRfq)
    {
        if (user == null)
        {
            return false;
        }

        if (forRfq)
        {
            return false;
        }

        return string.Equals(user.Role, "purchaser", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SupplierResponse> CreateSupplierAsync(CreateSupplierRequest request, string createdBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName) || string.IsNullOrWhiteSpace(request.CompanyId))
        {
            throw new ValidationException("Company name and company ID are required.");
        }

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var supplier = new Supplier
        {
            CompanyName = request.CompanyName.Trim(),
            CompanyId = request.CompanyId.Trim(),
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            Category = request.Category,
            Address = request.Address,
            Status = request.Status ?? "pending",
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
            Notes = request.Notes,
            BankAccount = request.BankAccount,
            PaymentTerms = request.PaymentTerms,
            CreditRating = request.CreditRating,
            ServiceCategory = request.ServiceCategory,
            Region = request.Region,
            Importance = request.Importance,
            ComplianceStatus = request.ComplianceStatus,
            ComplianceNotes = request.ComplianceNotes,
            ComplianceOwner = request.ComplianceOwner,
            FinancialContact = request.FinancialContact,
            PaymentCurrency = request.PaymentCurrency,
            FaxNumber = request.FaxNumber,
            BusinessRegistrationNumber = request.BusinessRegistrationNumber,
            SupplierCode = request.SupplierCode,
            Stage = "info_pending"
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        // 处理标签
        if (request.Tags?.Count > 0)
        {
            await SetSupplierTagsAsync(supplier.Id, request.Tags, cancellationToken);
        }

        // 记录审计日志
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "supplier",
            EntityId = supplier.Id.ToString(),
            Action = "create",
            ActorId = createdBy,
            ActorName = createdBy,
            Changes = System.Text.Json.JsonSerializer.Serialize(request)
        });

        return await EnrichSupplierAsync(supplier, false, cancellationToken);
    }

    public async Task<SupplierResponse?> UpdateSupplierAsync(int id, CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { id }, cancellationToken);
        if (supplier == null)
            return null;

        // 记录旧值用于审计
        var oldJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            supplier.CompanyName,
            supplier.CompanyId,
            supplier.ContactPerson,
            supplier.ContactPhone,
            supplier.ContactEmail,
            supplier.Category,
            supplier.Address,
            supplier.Status
        });

        // 更新字段
        supplier.CompanyName = request.CompanyName ?? supplier.CompanyName;
        supplier.CompanyId = request.CompanyId ?? supplier.CompanyId;
        supplier.ContactPerson = request.ContactPerson ?? supplier.ContactPerson;
        supplier.ContactPhone = request.ContactPhone ?? supplier.ContactPhone;
        supplier.ContactEmail = request.ContactEmail ?? supplier.ContactEmail;
        supplier.Category = request.Category ?? supplier.Category;
        supplier.Address = request.Address ?? supplier.Address;
        supplier.Notes = request.Notes ?? supplier.Notes;
        supplier.BankAccount = request.BankAccount ?? supplier.BankAccount;
        supplier.PaymentTerms = request.PaymentTerms ?? supplier.PaymentTerms;
        supplier.CreditRating = request.CreditRating ?? supplier.CreditRating;
        supplier.ServiceCategory = request.ServiceCategory ?? supplier.ServiceCategory;
        supplier.Region = request.Region ?? supplier.Region;
        supplier.Importance = request.Importance ?? supplier.Importance;
        supplier.ComplianceStatus = request.ComplianceStatus ?? supplier.ComplianceStatus;
        supplier.ComplianceNotes = request.ComplianceNotes ?? supplier.ComplianceNotes;
        supplier.ComplianceOwner = request.ComplianceOwner ?? supplier.ComplianceOwner;
        supplier.FinancialContact = request.FinancialContact ?? supplier.FinancialContact;
        supplier.PaymentCurrency = request.PaymentCurrency ?? supplier.PaymentCurrency;
        supplier.FaxNumber = request.FaxNumber ?? supplier.FaxNumber;
        supplier.BusinessRegistrationNumber = request.BusinessRegistrationNumber ?? supplier.BusinessRegistrationNumber;
        supplier.SupplierCode = request.SupplierCode ?? supplier.SupplierCode;
        supplier.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        await _context.SaveChangesAsync(cancellationToken);

        // 更新标签
        if (request.Tags != null)
        {
            await SetSupplierTagsAsync(id, request.Tags, cancellationToken);
        }

        // 记录审计日志
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "supplier",
            EntityId = id.ToString(),
            Action = "update",
            ActorId = "system",
            Changes = $"{{ old: {oldJson}, new: {System.Text.Json.JsonSerializer.Serialize(request)} }}"
        });

        return await EnrichSupplierAsync(supplier, false, cancellationToken);
    }

    public async Task<SupplierResponse?> UpdateSupplierStatusAsync(int id, UpdateSupplierStatusRequest request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { id }, cancellationToken);
        if (supplier == null)
            return null;

        supplier.Status = request.Status;
        supplier.CurrentApprover = request.CurrentApprover;
        supplier.Notes = request.Notes;
        supplier.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        await _context.SaveChangesAsync(cancellationToken);

        // 记录审批历史
        _context.Set<ApprovalHistory>().Add(new ApprovalHistory
        {
            SupplierId = id,
            Step = request.Status ?? "unknown",
            Approver = request.CurrentApprover,
            Result = request.Status,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Comments = request.Notes
        });
        await _context.SaveChangesAsync(cancellationToken);

        return await EnrichSupplierAsync(supplier, false, cancellationToken);
    }

    public async Task<SupplierResponse?> ApproveSupplierAsync(int id, ApproveSupplierRequest request, string approver, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { id }, cancellationToken);
        if (supplier == null)
            return null;

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (request.Decision == "approved")
        {
            supplier.Status = "approved";
            supplier.CurrentApprover = approver;
        }
        else if (request.Decision == "rejected")
        {
            supplier.Status = "rejected";
            supplier.CurrentApprover = approver;
        }

        supplier.UpdatedAt = now;

        // 记录审批历史
        _context.Set<ApprovalHistory>().Add(new ApprovalHistory
        {
            SupplierId = id,
            Step = "approval",
            Approver = approver,
            Result = request.Decision,
            Date = now,
            Comments = request.Comments
        });
        await _context.SaveChangesAsync(cancellationToken);

        return await EnrichSupplierAsync(supplier, false, cancellationToken);
    }

    #endregion
}
