using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Services;

public sealed partial class SupplierService
{
    #region 文档管理

    public async Task<List<SupplierDocumentResponse>> GetSupplierDocumentsAsync(int supplierId, CancellationToken cancellationToken)
    {
        var documents = await _context.SupplierDocuments
            .AsNoTracking()
            .Where(d => d.SupplierId == supplierId)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(d => new SupplierDocumentResponse
        {
            Id = d.Id,
            SupplierId = d.SupplierId,
            DocType = d.DocType,
            StoredName = d.StoredName,
            OriginalName = d.OriginalName,
            UploadedAt = d.UploadedAt,
            UploadedBy = d.UploadedBy,
            ValidFrom = d.ValidFrom,
            ExpiresAt = d.ExpiresAt,
            Status = d.Status,
            Notes = d.Notes,
            FileSize = d.FileSize,
            Category = d.Category,
            IsRequired = d.IsRequired
        }).ToList();
    }

    public async Task<SupplierDocumentResponse> UploadDocumentAsync(
        int supplierId,
        string fileName,
        string storedName,
        long fileSize,
        string uploadedBy,
        UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { supplierId }, cancellationToken);
        if (supplier == null)
        {
            _logger.LogWarning("UploadDocumentAsync failed - supplier not found: {SupplierId}", supplierId);
            throw new HttpResponseException(404, "Supplier not found.");
        }

        var document = new SupplierDocument
        {
            SupplierId = supplierId,
            DocType = request.DocType,
            StoredName = storedName,
            OriginalName = fileName,
            FileSize = fileSize,
            UploadedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            UploadedBy = uploadedBy,
            ValidFrom = request.ValidFrom,
            ExpiresAt = request.ExpiresAt,
            Status = "active",
            Notes = request.Notes,
            Category = request.Category,
            IsRequired = request.IsRequired
        };

        _context.SupplierDocuments.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        // 记录审计日志
        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "supplier_document",
            EntityId = document.Id.ToString(),
            Action = "upload",
            ActorId = uploadedBy,
            Changes = $"{{ supplierId: {supplierId}, fileName: \"{fileName}\", docType: \"{request.DocType}\" }}"
        });

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

    public async Task<SupplierDocumentResponse?> UpdateDocumentAsync(
        int supplierId,
        int documentId,
        UpdateDocumentRequest request,
        string? storedName,
        string? originalName,
        long? fileSize,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        var document = await _context.SupplierDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.SupplierId == supplierId, cancellationToken);

        if (document == null)
        {
            return null;
        }

        var hasChanges = false;

        if (request.DocType != null)
        {
            document.DocType = request.DocType;
            hasChanges = true;
        }

        if (request.Category != null)
        {
            document.Category = request.Category;
            hasChanges = true;
        }

        if (request.ValidFrom != null)
        {
            document.ValidFrom = request.ValidFrom;
            hasChanges = true;
        }

        if (request.ExpiresAt != null)
        {
            document.ExpiresAt = request.ExpiresAt;
            hasChanges = true;
        }

        if (request.Status != null)
        {
            document.Status = request.Status;
            hasChanges = true;
        }

        if (request.Notes != null)
        {
            document.Notes = request.Notes;
            hasChanges = true;
        }

        if (request.IsRequired.HasValue)
        {
            document.IsRequired = request.IsRequired.Value;
            hasChanges = true;
        }

        if (request.UploadedBy != null && string.IsNullOrWhiteSpace(storedName))
        {
            document.UploadedBy = request.UploadedBy;
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(storedName))
        {
            document.StoredName = storedName;
            document.OriginalName = originalName ?? document.OriginalName;
            document.FileSize = fileSize ?? document.FileSize;
            document.UploadedAt = DateTime.UtcNow.ToString("o");
            document.UploadedBy = request.UploadedBy ?? updatedBy;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "supplier_document",
                EntityId = document.Id.ToString(),
                Action = "update",
                ActorId = updatedBy,
                Changes = $"{{ supplierId: {supplierId}, docType: \"{document.DocType}\" }}"
            });
        }

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

    public async Task<SupplierDocumentResponse?> RenewDocumentAsync(
        int supplierId,
        int documentId,
        string storedName,
        string originalName,
        long fileSize,
        string uploadedBy,
        CancellationToken cancellationToken)
    {
        var document = await _context.SupplierDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.SupplierId == supplierId, cancellationToken);

        if (document == null)
        {
            return null;
        }

        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var newExpiresAt = string.IsNullOrWhiteSpace(document.ExpiresAt)
            ? null
            : DateTime.UtcNow.AddYears(1).ToString("o");

        document.StoredName = storedName;
        document.OriginalName = originalName;
        document.FileSize = fileSize;
        document.UploadedAt = now;
        document.UploadedBy = uploadedBy;
        document.Status = "pending";
        document.ExpiresAt = newExpiresAt;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "supplier_document",
            EntityId = document.Id.ToString(),
            Action = "renew",
            ActorId = uploadedBy,
            Changes = $"{{ supplierId: {supplierId}, docType: \"{document.DocType}\", fileName: \"{originalName}\" }}"
        });

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

    public async Task<bool> DeleteDocumentAsync(int supplierId, int documentId, CancellationToken cancellationToken)
    {
        var document = await _context.SupplierDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.SupplierId == supplierId, cancellationToken);

        if (document == null)
            return false;

        _context.SupplierDocuments.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<SupplierDocumentResponse?> GetDocumentDownloadInfoAsync(int supplierId, int documentId, CancellationToken cancellationToken)
    {
        var document = await _context.SupplierDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.SupplierId == supplierId, cancellationToken);

        if (document == null)
            return null;

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

    #endregion
}
