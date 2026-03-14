using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.DTOs.Suppliers;
using SupplierSystem.Application.Exceptions;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Common;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/suppliers/{supplierId:int}/documents")]
public sealed class SupplierDocumentsController : ApiControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IWebHostEnvironment _environment;

    public SupplierDocumentsController(ISupplierService supplierService, IWebHostEnvironment environment)
    {
        _supplierService = supplierService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> ListDocuments(int supplierId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanAccessDocuments(user, supplierId))
        {
            return Forbidden();
        }

        var supplier = await _supplierService.GetByIdAsync(supplierId, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        var documents = await _supplierService.GetSupplierDocumentsAsync(supplierId, cancellationToken);
        return Success(documents);
    }

    [HttpPost]
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
            return BadRequest("File is required.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return Forbidden();
        }

        var storedName = BuildStoredName(file.FileName);
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var filePath = Path.Combine(documentsRoot, storedName);

        try
        {
            await WriteUploadedFileAsync(file, filePath, cancellationToken);

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
                    IsRequired = isRequired,
                },
                cancellationToken);

            return Created(document);
        }
        catch (ServiceException ex)
        {
            DeleteFileIfExists(filePath);
            return StatusCode(ex.StatusCode, new ApiErrorResponse { Error = ex.Message, Code = ex.ErrorCode });
        }
        catch (Exception)
        {
            DeleteFileIfExists(filePath);
            return InternalError("Failed to upload document.");
        }
    }

    [HttpGet("{docId:int}/download")]
    public async Task<IActionResult> DownloadDocument(int supplierId, int docId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanAccessDocuments(user, supplierId))
        {
            return Forbidden();
        }

        var document = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (document == null)
        {
            return NotFound("Document not found.");
        }

        var storedName = document.StoredName;
        if (string.IsNullOrWhiteSpace(storedName))
        {
            return NotFound("Document file not found.");
        }

        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var filePath = Path.Combine(documentsRoot, storedName);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("Document file not found.");
        }

        var downloadName = string.IsNullOrWhiteSpace(document.OriginalName) ? storedName : document.OriginalName;
        return PhysicalFile(filePath, "application/octet-stream", downloadName);
    }

    [HttpPut("{docId:int}")]
    [HttpPost("{docId:int}")]
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
            return BadRequest("File is empty.");
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
            return BadRequest("No changes provided.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return Forbidden();
        }

        if (IsInvalidDateRange(validFrom, expiresAt))
        {
            return BadRequest("Valid from date must be earlier than expires at date.");
        }

        var existing = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (existing == null)
        {
            return NotFound("Document not found.");
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
                newStoredName = BuildStoredName(file.FileName);
                newOriginalName = file.FileName;
                newFileSize = file.Length;
                newFilePath = Path.Combine(documentsRoot, newStoredName);
                await WriteUploadedFileAsync(file, newFilePath, cancellationToken);
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
                    IsRequired = isRequired,
                },
                newStoredName,
                newOriginalName,
                newFileSize,
                user.Name ?? user.Id,
                cancellationToken);

            if (updated == null)
            {
                DeleteFileIfExists(newFilePath);
                return NotFound("Document not found.");
            }

            if (!string.IsNullOrWhiteSpace(newStoredName) &&
                !string.IsNullOrWhiteSpace(existing.StoredName) &&
                !string.Equals(existing.StoredName, newStoredName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteFileIfExists(Path.Combine(documentsRoot, existing.StoredName));
            }

            return Success(updated);
        }
        catch (ServiceException ex)
        {
            DeleteFileIfExists(newFilePath);
            return StatusCode(ex.StatusCode, new ApiErrorResponse { Error = ex.Message, Code = ex.ErrorCode });
        }
        catch (Exception)
        {
            DeleteFileIfExists(newFilePath);
            return InternalError("Failed to update document.");
        }
    }

    [HttpDelete("{docId:int}")]
    public async Task<IActionResult> DeleteDocument(int supplierId, int docId, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return Forbidden();
        }

        var document = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (document == null)
        {
            return NotFound("Document not found.");
        }

        var deleted = await _supplierService.DeleteDocumentAsync(supplierId, docId, cancellationToken);
        if (!deleted)
        {
            return NotFound("Document not found.");
        }

        if (!string.IsNullOrWhiteSpace(document.StoredName))
        {
            var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
            DeleteFileIfExists(Path.Combine(documentsRoot, document.StoredName));
        }

        return NoContent();
    }

    [HttpPost("{docId:int}/renew")]
    public async Task<IActionResult> RenewDocument(
        int supplierId,
        int docId,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return Forbidden();
        }

        var existing = await _supplierService.GetDocumentDownloadInfoAsync(supplierId, docId, cancellationToken);
        if (existing == null)
        {
            return NotFound("Document not found.");
        }

        var storedName = BuildStoredName(file.FileName);
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);
        var filePath = Path.Combine(documentsRoot, storedName);

        try
        {
            await WriteUploadedFileAsync(file, filePath, cancellationToken);

            var updated = await _supplierService.RenewDocumentAsync(
                supplierId,
                docId,
                storedName,
                file.FileName,
                file.Length,
                user.Name ?? user.Id,
                cancellationToken);

            if (updated == null)
            {
                DeleteFileIfExists(filePath);
                return NotFound("Document not found.");
            }

            if (!string.IsNullOrWhiteSpace(existing.StoredName))
            {
                DeleteFileIfExists(Path.Combine(documentsRoot, existing.StoredName));
            }

            return Success(updated);
        }
        catch (ServiceException ex)
        {
            DeleteFileIfExists(filePath);
            return StatusCode(ex.StatusCode, new ApiErrorResponse { Error = ex.Message, Code = ex.ErrorCode });
        }
        catch (Exception)
        {
            DeleteFileIfExists(filePath);
            return InternalError("Failed to renew document.");
        }
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> BulkUploadDocuments(
        int supplierId,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest("No files uploaded.");
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DocumentAccess.CanUploadDocuments(user, supplierId))
        {
            return Forbidden();
        }

        var supplier = await _supplierService.GetByIdAsync(supplierId, cancellationToken);
        if (supplier == null)
        {
            return NotFound("Supplier not found.");
        }

        var docTypes = Request.Form["docTypes"];
        var categories = Request.Form["categories"];
        var validFromValues = Request.Form["validFrom"];
        var expiresAtValues = Request.Form["expiresAt"];

        var success = new List<SupplierDocumentResponse>();
        var failed = new List<object>();
        var documentsRoot = UploadPathHelper.GetDocumentsRoot(_environment);

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var docType = GetFormValue(docTypes, index);
            var category = GetFormValue(categories, index);
            var validFrom = GetFormValue(validFromValues, index);
            var expiresAt = GetFormValue(expiresAtValues, index);

            if (string.IsNullOrWhiteSpace(docType))
            {
                failed.Add(new { filename = file.FileName, error = "Missing document type" });
                continue;
            }

            if (IsInvalidDateRange(validFrom, expiresAt))
            {
                failed.Add(new { filename = file.FileName, error = "Valid from date must be earlier than expires at date" });
                continue;
            }

            var storedName = BuildStoredName(file.FileName);
            var filePath = Path.Combine(documentsRoot, storedName);

            try
            {
                await WriteUploadedFileAsync(file, filePath, cancellationToken);
            }
            catch (Exception)
            {
                DeleteFileIfExists(filePath);
                failed.Add(new { filename = file.FileName, error = "Failed to store document." });
                continue;
            }

            try
            {
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
                        Notes = null,
                        IsRequired = false,
                    },
                    cancellationToken);

                success.Add(document);
            }
            catch (ServiceException ex)
            {
                DeleteFileIfExists(filePath);
                failed.Add(new { filename = file.FileName, error = ex.Message });
            }
            catch (Exception)
            {
                DeleteFileIfExists(filePath);
                failed.Add(new { filename = file.FileName, error = "Failed to upload document." });
            }
        }

        return Success(new { success, failed });
    }

    private static string BuildStoredName(string fileName)
    {
        return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
    }

    private static async Task WriteUploadedFileAsync(IFormFile file, string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);
    }

    private static void DeleteFileIfExists(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private static string? GetFormValue(IReadOnlyList<string> values, int index)
    {
        if (values.Count == 0)
        {
            return null;
        }

        if (index < values.Count)
        {
            return values[index];
        }

        return values[0];
    }

    private static bool IsInvalidDateRange(string? validFrom, string? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(expiresAt))
        {
            return false;
        }

        if (!DateTime.TryParse(validFrom, out var validFromDate))
        {
            return false;
        }

        if (!DateTime.TryParse(expiresAt, out var expiresAtDate))
        {
            return false;
        }

        return validFromDate >= expiresAtDate;
    }
}
