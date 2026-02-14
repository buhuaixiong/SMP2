using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Files;
using SupplierSystem.Api.Services.FileUploads;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[NodeResponse]
[Route("api/file-uploads")]
public sealed class FileUploadsController : ControllerBase
{
    private const long MaxFileSize = 20L * 1024 * 1024;
    private readonly FileUploadService _service;
    private readonly FileUploadReminderService _reminderService;
    private readonly SupplierFileRepository _fileRepository;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadsController> _logger;

    public FileUploadsController(
        FileUploadService service,
        FileUploadReminderService reminderService,
        SupplierFileRepository fileRepository,
        CompatibilityMigrationService migrationService,
        IWebHostEnvironment environment,
        ILogger<FileUploadsController> logger)
    {
        _service = service;
        _reminderService = reminderService;
        _fileRepository = fileRepository;
        _migrationService = migrationService;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? supplierId,
        [FromForm] string? fileDescription,
        [FromForm] string? validFrom,
        [FromForm] string? validTo,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (file == null)
        {
            return BadRequest(new { message = "No file uploaded" });
        }

        if (!int.TryParse((supplierId ?? string.Empty).Trim(), out var supplierIdValue))
        {
            return BadRequest(new { message = "supplierId is required" });
        }

        if (string.IsNullOrWhiteSpace(validFrom) || string.IsNullOrWhiteSpace(validTo))
        {
            return BadRequest(new { message = "File validity period is required (validFrom and validTo)" });
        }

        var periodError = ValidateValidityPeriod(validFrom, validTo);
        if (periodError != null)
        {
            return BadRequest(new { message = periodError });
        }

        if (file.Length > MaxFileSize)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "File exceeds the 20 MB size limit." });
        }

        var validationError = ValidateUpload(file);
        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var supplierRoot = UploadPathHelper.GetSupplierFilesRoot(_environment);
        var supplierDir = Path.Combine(supplierRoot, supplierIdValue.ToString());
        Directory.CreateDirectory(supplierDir);

        var storedName = FileUploadRules.BuildStoredName(file.FileName);
        var filePath = Path.Combine(supplierDir, storedName);

        try
        {
            await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            SafeDelete(filePath);
            _logger.LogError(ex, "Failed to save uploaded file.");
            return StatusCode(500, new { message = "Failed to process uploaded file." });
        }

        try
        {
            await _migrationService.EnsureMigratedAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow.ToString("o");
            var fileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            var record = new SupplierFileRecord
            {
                AgreementNumber = null,
                FileType = string.IsNullOrWhiteSpace(fileType) ? null : fileType,
                ValidFrom = NormalizeDate(validFrom),
                ValidTo = NormalizeDate(validTo),
                SupplierId = supplierIdValue,
                Status = "pending",
                UploadTime = now,
                UploaderName = user.Name,
                OriginalName = file.FileName,
                StoredName = storedName
            };

            var fileId = await _fileRepository.CreateFileAsync(record, cancellationToken);
            var result = await _service.CreateFileUploadAsync(
                user,
                supplierIdValue,
                fileId,
                file.FileName,
                fileDescription,
                validFrom,
                validTo,
                cancellationToken);

            var message = result.TryGetValue("message", out var value) ? value?.ToString() : null;
            return StatusCode(201, new
            {
                message = message ?? "File uploaded successfully.",
                data = result
            });
        }
        catch (FileUploadServiceException ex)
        {
            SafeDelete(filePath);
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            SafeDelete(filePath);
            _logger.LogError(ex, "Error creating file upload.");
            return StatusCode(400, new { message = ex.Message ?? "Failed to upload file" });
        }
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        var limitValue = NormalizeLimit(limit, 50);
        var offsetValue = NormalizeOffset(offset);

        try
        {
            var uploads = await _service.GetPendingFileApprovalsAsync(user, limitValue, offsetValue, cancellationToken);
            return Ok(new
            {
                data = uploads,
                pagination = new
                {
                    limit = limitValue,
                    offset = offsetValue,
                    total = uploads.Count
                }
            });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending file approvals.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch pending approvals" });
        }
    }

    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedApprovals(
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        var limitValue = NormalizeLimit(limit, 50);
        var offsetValue = NormalizeOffset(offset);

        try
        {
            var uploads = await _service.GetApprovedFileApprovalsAsync(user, limitValue, offsetValue, cancellationToken);
            return Ok(new
            {
                data = uploads,
                pagination = new
                {
                    limit = limitValue,
                    offset = offsetValue,
                    total = uploads.Count
                }
            });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching approved file approvals.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch approved approvals" });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetails(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid upload ID" });
        }

        try
        {
            var details = await _service.GetFileUploadDetailsAsync(user, id, cancellationToken);
            return Ok(new { data = details });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching file upload details.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch file upload details" });
        }
    }

    [HttpPut("{id:int}/approve")]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] FileUploadApprovalRequest? request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid upload ID" });
        }

        var decision = request?.Decision;
        if (string.IsNullOrWhiteSpace(decision) ||
            (!string.Equals(decision, "approved", StringComparison.Ordinal) &&
             !string.Equals(decision, "rejected", StringComparison.Ordinal)))
        {
            return BadRequest(new { message = "Decision must be \"approved\" or \"rejected\"" });
        }

        try
        {
            var result = await _service.ApproveFileUploadAsync(
                user,
                id,
                decision,
                request?.Comments ?? string.Empty,
                cancellationToken);
            var message = result.TryGetValue("message", out var value) ? value?.ToString() : null;
            return Ok(new
            {
                message = message,
                data = result
            });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving file upload.");
            return StatusCode(400, new { message = ex.Message ?? "Failed to approve file upload" });
        }
    }

    [HttpGet("supplier/{supplierId:int}")]
    public async Task<IActionResult> GetSupplierUploads(
        int supplierId,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (supplierId <= 0)
        {
            return BadRequest(new { message = "Invalid supplier ID" });
        }

        var limitValue = NormalizeLimit(limit, 50);
        var offsetValue = NormalizeOffset(offset);

        try
        {
            var uploads = await _service.GetSupplierFileUploadsAsync(user, supplierId, limitValue, offsetValue, cancellationToken);
            return Ok(new
            {
                data = uploads,
                pagination = new
                {
                    limit = limitValue,
                    offset = offsetValue,
                    total = uploads.Count
                }
            });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching supplier file uploads.");
            var statusCode = ex.Message?.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0 ? 403 : 500;
            return StatusCode(statusCode, new { message = ex.Message ?? "Failed to fetch supplier file uploads" });
        }
    }

    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiringFiles(
        [FromQuery] int? daysThreshold,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        var threshold = daysThreshold ?? 7;

        try
        {
            var files = await _service.GetExpiringFilesAsync(user, threshold, cancellationToken);
            return Ok(new { data = files });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expiring files.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to fetch expiring files" });
        }
    }

    [HttpPost("{id:int}/send-reminder")]
    public async Task<IActionResult> SendReminder(int id, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid upload ID" });
        }

        try
        {
            var result = await _reminderService.SendSingleFileReminderAsync(user, id, cancellationToken);
            var message = result.TryGetValue("message", out var value) ? value?.ToString() : null;
            return Ok(new
            {
                message = message,
                data = result
            });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending file reminder.");
            return StatusCode(400, new { message = ex.Message ?? "Failed to send reminder" });
        }
    }

    [HttpPost("batch-reminder")]
    public async Task<IActionResult> SendBatchReminders(
        [FromBody] BatchReminderRequest? request,
        CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required" });
        }

        var threshold = request?.DaysThreshold ?? 7;

        try
        {
            var result = await _reminderService.SendBatchFileRemindersAsync(user, threshold, cancellationToken);
            var message = result.TryGetValue("message", out var value) ? value?.ToString() : null;
            return Ok(new
            {
                message = message,
                data = result
            });
        }
        catch (FileUploadServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending batch reminders.");
            return StatusCode(500, new { message = ex.Message ?? "Failed to send batch reminders" });
        }
    }

    private static string? ValidateUpload(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        if (!IsAllowedExtension(extension))
        {
            return "Unsupported file format. Allowed: pdf, doc, docx, xls, xlsx, jpg, jpeg, png.";
        }

        var mime = file.ContentType ?? string.Empty;
        if (string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return "Generic binary files (application/octet-stream) are not allowed.";
        }

        if (!FileUploadRules.AllowedMimeTypes.Contains(mime))
        {
            return "Unsupported file type. Allowed: pdf, doc, docx, xls, xlsx, jpg, jpeg, png.";
        }

        return null;
    }

    private static string? ValidateValidityPeriod(string validFrom, string validTo)
    {
        if (!DateTimeOffset.TryParse(validFrom, out var fromDate))
        {
            return "Invalid validFrom date format";
        }

        if (!DateTimeOffset.TryParse(validTo, out var toDate))
        {
            return "Invalid validTo date format";
        }

        var today = DateTimeOffset.Now.Date;
        if (fromDate.Date < today)
        {
            return "validFrom date cannot be in the past";
        }

        if (toDate.Date <= fromDate.Date)
        {
            return "validTo date must be after validFrom date";
        }

        return null;
    }

    private static bool IsAllowedExtension(string extension)
    {
        if (FileUploadRules.AllowedExtensions.Contains(extension))
        {
            return true;
        }

        return string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            var local = parsed.DateTime;
            return local.ToString("yyyy-MM-ddTHH:mm:ss");
        }

        return null;
    }

    private static void SafeDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static int NormalizeLimit(int? limit, int fallback)
    {
        if (!limit.HasValue)
        {
            return fallback;
        }

        return limit.Value < 0 ? 0 : limit.Value;
    }

    private static int NormalizeOffset(int? offset)
    {
        if (!offset.HasValue || offset.Value < 0)
        {
            return 0;
        }

        return offset.Value;
    }

    public sealed class FileUploadApprovalRequest
    {
        public string? Decision { get; set; }
        public string? Comments { get; set; }
    }

    public sealed class BatchReminderRequest
    {
        public int? DaysThreshold { get; set; }
    }
}

