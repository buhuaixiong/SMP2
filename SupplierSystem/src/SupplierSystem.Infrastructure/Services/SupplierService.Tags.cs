using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 标签管理

    public async Task<List<TagResponse>> GetTagsAsync(CancellationToken cancellationToken)
    {
        var tags = await _context.TagDefs
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tags.Select(t => new TagResponse
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            Color = t.Color
        }).ToList();
    }

    public async Task<TagResponse> CreateTagAsync(CreateTagRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedName))
        {
            _logger.LogWarning("CreateTagAsync failed - tag name is required");
            throw new HttpResponseException(400, "Tag name is required.");
        }

        // 检查是否已存在
        var existing = await _context.TagDefs
            .FirstOrDefaultAsync(t => t.Name == normalizedName, cancellationToken);

        if (existing != null)
        {
            _logger.LogWarning("CreateTagAsync failed - tag with name '{TagName}' already exists", normalizedName);
            throw new HttpResponseException(409, "Tag with this name already exists.");
        }

        var tag = new TagDef
        {
            Name = normalizedName,
            Description = request.Description,
            Color = request.Color,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        _context.TagDefs.Add(tag);
        await _context.SaveChangesAsync(cancellationToken);

        return new TagResponse
        {
            Id = tag.Id,
            Name = tag.Name,
            Description = tag.Description,
            Color = tag.Color
        };
    }

    public async Task<TagResponse?> UpdateTagAsync(int id, UpdateTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await _context.TagDefs.FindAsync(new object[] { id }, cancellationToken);
        if (tag == null)
            return null;

        if (request.Name != null)
            tag.Name = request.Name.Trim().ToLowerInvariant();
        tag.Description = request.Description ?? tag.Description;
        tag.Color = request.Color ?? tag.Color;

        await _context.SaveChangesAsync(cancellationToken);

        return new TagResponse
        {
            Id = tag.Id,
            Name = tag.Name,
            Description = tag.Description,
            Color = tag.Color
        };
    }

    public async Task<bool> DeleteTagAsync(int id, CancellationToken cancellationToken)
    {
        var tag = await _context.TagDefs.FindAsync(new object[] { id }, cancellationToken);
        if (tag == null)
            return false;

        // 删除关联的供应商标签
        _context.SupplierTags.RemoveRange(_context.SupplierTags.Where(st => st.TagId == id));

        _context.TagDefs.Remove(tag);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<BatchTagResult> BatchAssignTagAsync(int tagId, List<int> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0)
            return new BatchTagResult { Added = 0, Skipped = 0 };

        var result = new BatchTagResult();
        var existingRelations = await _context.SupplierTags
            .Where(st => st.TagId == tagId && supplierIds.Contains(st.SupplierId))
            .ToListAsync(cancellationToken);

        var existingSupplierIds = existingRelations.Select(st => st.SupplierId).ToHashSet();
        var suppliersToAdd = supplierIds.Where(id => !existingSupplierIds.Contains(id)).ToList();

        foreach (var supplierId in suppliersToAdd)
        {
            _context.SupplierTags.Add(new SupplierTag
            {
                SupplierId = supplierId,
                TagId = tagId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        result.Added = suppliersToAdd.Count;
        result.Skipped = supplierIds.Count - suppliersToAdd.Count;

        return result;
    }

    public async Task<BatchTagResult> BatchRemoveTagAsync(int tagId, List<int> supplierIds, CancellationToken cancellationToken)
    {
        if (supplierIds.Count == 0)
            return new BatchTagResult { Removed = 0 };

        var relations = await _context.SupplierTags
            .Where(st => st.TagId == tagId && supplierIds.Contains(st.SupplierId))
            .ToListAsync(cancellationToken);

        _context.SupplierTags.RemoveRange(relations);
        await _context.SaveChangesAsync(cancellationToken);

        return new BatchTagResult { Removed = relations.Count };
    }

    public async Task<List<SupplierResponse>> GetSuppliersByTagAsync(int tagId, CancellationToken cancellationToken)
    {
        var suppliers = await _context.Suppliers
            .AsNoTracking()
            .Join(_context.SupplierTags.Where(st => st.TagId == tagId),
                  s => s.Id, st => st.SupplierId, (s, st) => s)
            .OrderBy(s => s.CompanyName)
            .ToListAsync(cancellationToken);

        var enrichedSuppliers = new List<SupplierResponse>(suppliers.Count);
        foreach (var supplier in suppliers)
        {
            enrichedSuppliers.Add(await EnrichSupplierAsync(supplier, false, cancellationToken));
        }

        return enrichedSuppliers;
    }

    public async Task<List<TagResponse>> UpdateSupplierTagsAsync(int id, List<string> tags, CancellationToken cancellationToken)
    {
        return await SetSupplierTagsAsync(id, tags, cancellationToken);
    }

    #endregion
}
