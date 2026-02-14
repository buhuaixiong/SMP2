using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers/documents")]
public sealed class SupplierDocumentsController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IWebHostEnvironment _environment;

    public SupplierDocumentsController(ISupplierService supplierService, IWebHostEnvironment environment)
    {
        _supplierService = supplierService;
        _environment = environment;
    }

    [HttpGet("{supplierId:int}")]
    public async Task<IActionResult> ListDocuments(int supplierId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!DocumentAccess.CanAccessDocuments(user, supplierId))
        {
            return StatusCode(403, new { message = "Access denied to supplier documents." });
        }

        var documents = await _supplierService.GetSupplierDocumentsAsync(supplierId, cancellationToken);
        return Ok(new { data = documents });
    }

    [HttpPost("{supplierId:int}")]
    public async Task<IActionResult> UploadDocument(
        int supplierId,
        IFormFile file,
        [FromForm] string? docType,
        [FromForm] string? category,
        [FromForm] string? validFrom,
        [FromForm] string? expiresAt,
        [FromForm] string? notes,
        [FromForm] bool isRequired = false,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return StatusCode(403, new { message = "Access denied to upload documents." });
        }

        var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var filePath = Path.Combine(documentsRoot, storedName);

        try
        {
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var document = await _supplierService.UploadDocumentAsync(
                supplierId,
                file.FileName,
                storedName,
                file.Length,
                user.Name ?? user.Id,
                new UploadDocumentRequest
                {
                    DocType = docType,
                    Category = category,
                    ValidFrom = validFrom,
                    ExpiresAt = expiresAt,
                    Notes = notes,
                    IsRequired = isRequired
                },
                cancellationToken);

            return StatusCode(201, new { data = document });
        }
        catch (HttpResponseException ex)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
        catch (Exception)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return StatusCode(500, new { message = "Failed to upload document." });
        }
    }

    [HttpGet("{supplierId:int}/{docId:int}/download")]
    public async Task<IActionResult> DownloadDocument(int supplierId, int docId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!DocumentAccess.CanAccessDocuments(user, supplierId))
        {
            return StatusCode(403, new { message = "Access denied to supplier documents." });
        }

        var document = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (document == null)
        {
            return NotFound(new { message = "Document not found." });
        }

        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var storedName = document.StoredName;
        if (string.IsNullOrWhiteSpace(storedName))
        {
            return NotFound(new { message = "Document file not found." });
        }

        var filePath = Path.Combine(documentsRoot, storedName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "Document file not found." });
        }

        var downloadName = string.IsNullOrWhiteSpace(document.OriginalName) ? storedName : document.OriginalName;
        return PhysicalFile(filePath, "application/octet-stream", downloadName);
    }

    [HttpPut("{supplierId:int}/{docId:int}")]
    public async Task<IActionResult> UpdateDocument(
        int supplierId,
        int docId,
        IFormFile? file,
        [FromForm] string? docType,
        [FromForm] string? category,
        [FromForm] string? validFrom,
        [FromForm] string? expiresAt,
        [FromForm] string? status,
        [FromForm] string? notes,
        [FromForm] string? uploadedBy,
        [FromForm] bool? isRequired,
        CancellationToken cancellationToken = default)
    {
        if (file != null && file.Length == 0)
        {
            return BadRequest(new { message = "File is empty." });
        }

        var hasMetadataUpdates =
            docType != null ||
            category != null ||
            validFrom != null ||
            expiresAt != null ||
            status != null ||
            notes != null ||
            uploadedBy != null ||
            isRequired.HasValue;

        if (file == null && !hasMetadataUpdates)
        {
            return BadRequest(new { message = "No changes provided." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return StatusCode(403, new { message = "Access denied to modify documents." });
        }

        if (!string.IsNullOrWhiteSpace(validFrom) && !string.IsNullOrWhiteSpace(expiresAt)
            && DateTime.TryParse(validFrom, out var validFromDate)
            && DateTime.TryParse(expiresAt, out var expiresAtDate)
            && validFromDate >= expiresAtDate)
        {
            return BadRequest(new { message = "Valid from date must be earlier than expires at date." });
        }

        var existing = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (existing == null)
        {
            return NotFound(new { message = "Document not found." });
        }

        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        string? newStoredName = null;
        string? newOriginalName = null;
        long? newFileSize = null;
        string? newFilePath = null;

        try
        {
            if (file != null)
            {
                newStoredName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
                newOriginalName = file.FileName;
                newFileSize = file.Length;
                newFilePath = Path.Combine(documentsRoot, newStoredName);

                await using (var stream = new FileStream(newFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }
            }

            var updated = await _supplierService.UpdateDocumentAsync(
                supplierId,
                docId,
                new UpdateDocumentRequest
                {
                    DocType = docType,
                    Category = category,
                    ValidFrom = validFrom,
                    ExpiresAt = expiresAt,
                    Status = status,
                    Notes = notes,
                    UploadedBy = uploadedBy,
                    IsRequired = isRequired
                },
                newStoredName,
                newOriginalName,
                newFileSize,
                user.Name ?? user.Id,
                cancellationToken);

            if (updated == null)
            {
                if (newFilePath != null && System.IO.File.Exists(newFilePath))
                {
                    System.IO.File.Delete(newFilePath);
                }
                return NotFound(new { message = "Document not found." });
            }

            if (!string.IsNullOrWhiteSpace(newStoredName)
                && !string.IsNullOrWhiteSpace(existing.StoredName)
                && !string.Equals(existing.StoredName, newStoredName, StringComparison.OrdinalIgnoreCase))
            {
                var oldFilePath = Path.Combine(documentsRoot, existing.StoredName);
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            return Ok(new { data = updated });
        }
        catch (HttpResponseException ex)
        {
            if (newFilePath != null && System.IO.File.Exists(newFilePath))
            {
                System.IO.File.Delete(newFilePath);
            }

            return StatusCode(ex.Status, ex.Value ?? new { message = ex.Message });
        }
        catch (Exception)
        {
            if (newFilePath != null && System.IO.File.Exists(newFilePath))
            {
                System.IO.File.Delete(newFilePath);
            }

            return StatusCode(500, new { message = "Failed to update document." });
        }
    }

    [HttpDelete("{supplierId:int}/{docId:int}")]
    public async Task<IActionResult> DeleteDocument(int supplierId, int docId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return StatusCode(403, new { message = "Access denied to delete documents." });
        }

        var document = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (document == null)
        {
            return NotFound(new { message = "Document not found." });
        }

        var deleted = await _supplierService.DeleteDocumentAsync(supplierId, docId, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { message = "Document not found." });
        }

        if (!string.IsNullOrWhiteSpace(document.StoredName))
        {
            var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
            var filePath = Path.Combine(documentsRoot, document.StoredName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        return NoContent();
    }
}
