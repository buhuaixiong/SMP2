using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    public async Task<SupplierCompletenessUpdateResult> UpdateAllSuppliersCompletenessAsync(
        string triggeredBy,
        string triggerReason,
        CancellationToken cancellationToken)
    {
        var result = new SupplierCompletenessUpdateResult();
        var suppliers = await _context.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        result.Total = suppliers.Count;

        foreach (var supplier in suppliers)
        {
            try
            {
                await UpdateSupplierCompletenessAsync(supplier, triggeredBy, triggerReason, cancellationToken);
                result.Successful++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new SupplierCompletenessUpdateError
                {
                    SupplierId = supplier.Id,
                    Message = ex.Message
                });
            }
        }

        return result;
    }

    private async Task UpdateSupplierCompletenessAsync(
        Supplier supplier,
        string triggeredBy,
        string triggerReason,
        CancellationToken cancellationToken)
    {
        var documents = await GetSupplierDocumentsAsync(supplier.Id, cancellationToken);
        var whitelist = await GetWhitelistedDocumentTypesAsync(supplier.Id, cancellationToken);
        var compliance = BuildComplianceSummary(supplier, documents, whitelist);
        var now = DateTimeOffset.UtcNow.ToString("o");

        var updated = new Supplier
        {
            Id = supplier.Id,
            ProfileCompletion = compliance.ProfileScore,
            DocumentCompletion = compliance.DocumentScore,
            CompletionScore = compliance.OverallScore,
            CompletionStatus = compliance.CompletionCategory,
            CompletionLastUpdated = now
        };

        _context.Suppliers.Attach(updated);
        _context.Entry(updated).Property(s => s.ProfileCompletion).IsModified = true;
        _context.Entry(updated).Property(s => s.DocumentCompletion).IsModified = true;
        _context.Entry(updated).Property(s => s.CompletionScore).IsModified = true;
        _context.Entry(updated).Property(s => s.CompletionStatus).IsModified = true;
        _context.Entry(updated).Property(s => s.CompletionLastUpdated).IsModified = true;

        var history = new SupplierCompletionHistory
        {
            SupplierId = supplier.Id,
            ProfileCompletion = compliance.ProfileScore,
            DocumentCompletion = compliance.DocumentScore,
            CompletionScore = compliance.OverallScore,
            CompletionStatus = compliance.CompletionCategory ?? "unknown",
            MissingProfileFields = JsonSerializer.Serialize(compliance.MissingProfileFields.Select(field => field.Key)),
            MissingDocumentTypes = JsonSerializer.Serialize(compliance.MissingDocumentTypes.Select(doc => doc.Type)),
            TriggeredBy = triggeredBy,
            TriggerReason = triggerReason,
            RecordedAt = now
        };

        _context.SupplierCompletionHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
