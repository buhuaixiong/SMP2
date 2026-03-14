using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    private const int CompletenessBatchSize = 200;

    public async Task<SupplierCompletenessUpdateResult> UpdateAllSuppliersCompletenessAsync(
        string triggeredBy,
        string triggerReason,
        CancellationToken cancellationToken)
    {
        var result = new SupplierCompletenessUpdateResult();
        var suppliers = await _context.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        result.Total = suppliers.Count;
        if (suppliers.Count == 0)
        {
            return result;
        }

        var supplierIds = suppliers.Select(supplier => supplier.Id).ToArray();
        var documentsBySupplier = await PrefetchSupplierDocumentsAsync(supplierIds, cancellationToken);
        var whitelistedBySupplier = await PrefetchWhitelistedDocumentTypesAsync(supplierIds, cancellationToken);
        var batchEntries = new List<CompletenessBatchEntry>(CompletenessBatchSize);
        foreach (var supplier in suppliers)
        {
            try
            {
                documentsBySupplier.TryGetValue(supplier.Id, out var documents);
                whitelistedBySupplier.TryGetValue(supplier.Id, out var whitelist);

                var compliance = BuildComplianceSummary(
                    supplier,
                    documents ?? Array.Empty<SupplierDocumentResponse>(),
                    whitelist ?? Array.Empty<string>());
                var now = DateTimeOffset.UtcNow.ToString("o");

                batchEntries.Add(new CompletenessBatchEntry(
                    supplier.Id,
                    new Supplier
                    {
                        Id = supplier.Id,
                        ProfileCompletion = compliance.ProfileScore,
                        DocumentCompletion = compliance.DocumentScore,
                        CompletionScore = compliance.OverallScore,
                        CompletionStatus = compliance.CompletionCategory,
                        CompletionLastUpdated = now
                    },
                    new SupplierCompletionHistory
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
                    }));

                if (batchEntries.Count >= CompletenessBatchSize)
                {
                    await SaveCompletenessBatchAsync(batchEntries, result, cancellationToken);
                    batchEntries.Clear();
                }
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

        if (batchEntries.Count > 0)
        {
            await SaveCompletenessBatchAsync(batchEntries, result, cancellationToken);
            batchEntries.Clear();
        }

        return result;
    }

    private async Task<Dictionary<int, IReadOnlyList<SupplierDocumentResponse>>> PrefetchSupplierDocumentsAsync(
        IReadOnlyCollection<int> supplierIds,
        CancellationToken cancellationToken)
    {
        if (supplierIds == null || supplierIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<SupplierDocumentResponse>>();
        }

        var documents = await _context.SupplierDocuments
            .AsNoTracking()
            .Where(document => supplierIds.Contains(document.SupplierId))
            .OrderByDescending(document => document.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents
            .GroupBy(document => document.SupplierId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<SupplierDocumentResponse>)group.Select(MapSupplierDocument).ToList());
    }

    private async Task<Dictionary<int, IReadOnlyList<string>>> PrefetchWhitelistedDocumentTypesAsync(
        IReadOnlyCollection<int> supplierIds,
        CancellationToken cancellationToken)
    {
        if (supplierIds == null || supplierIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<string>>();
        }

        var now = DateTimeOffset.UtcNow;
        var entries = await _context.SupplierDocumentWhitelists
            .AsNoTracking()
            .Where(entry => supplierIds.Contains(entry.SupplierId) && entry.IsActive)
            .ToListAsync(cancellationToken);

        return entries
            .Where(entry =>
                string.IsNullOrWhiteSpace(entry.ExpiresAt) ||
                !TryParseDate(entry.ExpiresAt, out var expiresAt) ||
                expiresAt > now)
            .GroupBy(entry => entry.SupplierId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(entry => entry.DocumentType)
                    .Where(documentType => !string.IsNullOrWhiteSpace(documentType))
                    .ToList());
    }

    private async Task SaveCompletenessBatchAsync(
        IReadOnlyList<CompletenessBatchEntry> batchEntries,
        SupplierCompletenessUpdateResult result,
        CancellationToken cancellationToken)
    {
        if (batchEntries == null || batchEntries.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var entry in batchEntries)
            {
                _context.Suppliers.Attach(entry.Supplier);
                var entityEntry = _context.Entry(entry.Supplier);
                entityEntry.Property(supplier => supplier.ProfileCompletion).IsModified = true;
                entityEntry.Property(supplier => supplier.DocumentCompletion).IsModified = true;
                entityEntry.Property(supplier => supplier.CompletionScore).IsModified = true;
                entityEntry.Property(supplier => supplier.CompletionStatus).IsModified = true;
                entityEntry.Property(supplier => supplier.CompletionLastUpdated).IsModified = true;
            }

            _context.SupplierCompletionHistories.AddRange(batchEntries.Select(entry => entry.History));
            await _context.SaveChangesAsync(cancellationToken);
            result.Successful += batchEntries.Count;
        }
        catch (Exception ex)
        {
            foreach (var entry in batchEntries)
            {
                result.Failed++;
                result.Errors.Add(new SupplierCompletenessUpdateError
                {
                    SupplierId = entry.SupplierId,
                    Message = ex.Message
                });
            }
        }
        finally
        {
            _context.ChangeTracker.Clear();
        }
    }

    private static SupplierDocumentResponse MapSupplierDocument(SupplierDocument document)
    {
        return new SupplierDocumentResponse
        {
            Id = document.Id,
            SupplierId = document.SupplierId,
            DocType = document.DocType,
            StoredName = document.StoredName,
            OriginalName = document.OriginalName,
            UploadedAt = document.UploadedAt,
            UploadedBy = document.UploadedBy,
            ValidFrom = document.ValidFrom,
            ExpiresAt = document.ExpiresAt,
            Status = document.Status,
            Notes = document.Notes,
            FileSize = document.FileSize,
            Category = document.Category,
            IsRequired = document.IsRequired
        };
    }

    private sealed record CompletenessBatchEntry(
        int SupplierId,
        Supplier Supplier,
        SupplierCompletionHistory History);
}
