using Microsoft.EntityFrameworkCore;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 草稿管理

    public async Task<bool> SaveDraftAsync(int supplierId, object draftData, string updatedBy, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken);
        if (supplier == null)
            return false;

        var draftJson = System.Text.Json.JsonSerializer.Serialize(draftData);
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var existingDraft = await _context.SupplierDrafts
            .FirstOrDefaultAsync(d => d.SupplierId == supplierId, cancellationToken);

        if (existingDraft != null)
        {
            existingDraft.DraftData = draftJson;
            existingDraft.UpdatedAt = now;
            existingDraft.UpdatedBy = updatedBy;
        }
        else
        {
            _context.SupplierDrafts.Add(new SupplierDraft
            {
                SupplierId = supplierId,
                DraftData = draftJson,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = updatedBy,
                UpdatedBy = updatedBy
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<object?> GetDraftAsync(int supplierId, CancellationToken cancellationToken)
    {
        var draft = await _context.SupplierDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SupplierId == supplierId, cancellationToken);

        if (draft == null)
            return null;

        if (string.IsNullOrEmpty(draft.DraftData))
            return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<object>(draft.DraftData);
        }
        catch
        {
            return draft.DraftData;
        }
    }

    public async Task<bool> DeleteDraftAsync(int supplierId, CancellationToken cancellationToken)
    {
        var draft = await _context.SupplierDrafts
            .FirstOrDefaultAsync(d => d.SupplierId == supplierId, cancellationToken);

        if (draft == null)
            return false;

        _context.SupplierDrafts.Remove(draft);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    #endregion
}
