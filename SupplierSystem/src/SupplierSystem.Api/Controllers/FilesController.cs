using System.Data.Common;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Files;
using SupplierSystem.Api.Services.TempSuppliers;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

/// <summary>
/// 文件管理控制器
/// 处理供应商文件的上传、下载、元数据管理和访问控制
/// </summary>
[ApiController]
[Authorize]
[NodeResponse]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private const long MaxFileSize = 50L * 1024 * 1024;
    private const int DefaultDownloadTokenMinutes = 30;
    private const int MaxDownloadTokenMinutes = 1440;
    private const string DefaultResourceType = "supplier_file";
    private const string DefaultTokenCategory = "suppliers";
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();
    private readonly SupplierSystemDbContext _dbContext;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly TempSupplierUpgradeRepository _upgradeRepository;
    private readonly TempSupplierUpgradeService _upgradeService;
    private readonly SupplierFileRepository _fileRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        SupplierSystemDbContext dbContext,
        CompatibilityMigrationService migrationService,
        WorkflowEngine workflowEngine,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<FilesController> logger)
    {
        _dbContext = dbContext;
        _migrationService = migrationService;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _upgradeRepository = new TempSupplierUpgradeRepository(dbContext);
        _fileRepository = new SupplierFileRepository(dbContext);
        _upgradeService = new TempSupplierUpgradeService(dbContext, migrationService, workflowEngine, _upgradeRepository);
    }

    /// <summary>
    /// 上传供应商文件
    /// </summary>
    /// <param name="file">上传的文件</param>
    /// <param name="supplierId">供应商ID</param>
    /// <param name="agreementNumber">协议编号</param>
    /// <param name="fileType">文件类型</param>
    /// <param name="validFrom">有效期开始日期</param>
    /// <param name="validTo">有效期结束日期</param>
    /// <param name="status">文件状态</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>上传结果</returns>
    /// <response code="201">文件上传成功</response>
    /// <response code="400">请求参数错误（缺少文件或supplierId）</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限上传该供应商的文件</response>
    /// <response code="413">文件大小超过限制（50MB）</response>
    /// <response code="422">文件未通过合规扫描</response>
    /// <response code="500">服务器内部错误</response>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? supplierId,
        [FromForm] string? agreementNumber,
        [FromForm] string? fileType,
        [FromForm] string? validFrom,
        [FromForm] string? validTo,
        [FromForm] string? status,
        CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return BadRequest(new { message = "File is required." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!int.TryParse((supplierId ?? string.Empty).Trim(), out var supplierIdValue))
        {
            return BadRequest(new { message = "supplierId is required." });
        }

        if (!CanUploadFiles(user, supplierIdValue))
        {
            return StatusCode(403, new { message = "Access denied to upload files for this supplier." });
        }

        if (file.Length > MaxFileSize)
        {
            return StatusCode(413, new { message = "File exceeds the 50 MB size limit." });
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
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            SafeDelete(filePath);
            _logger.LogError(ex, "Failed to move uploaded file.");
            return StatusCode(500, new { message = "Failed to process uploaded file." });
        }

        try
        {
            await ScanWithClamAvAsync(filePath, file.FileName, cancellationToken);
        }
        catch (Exception ex)
        {
            SafeDelete(filePath);
            _logger.LogError(ex, "ClamAV scan blocked file upload.");
            return StatusCode(422, new { message = ex.Message ?? "File failed compliance scan." });
        }

        try
        {
            await _migrationService.EnsureMigratedAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow.ToString("o");
            var record = new SupplierFileRecord
            {
                AgreementNumber = string.IsNullOrWhiteSpace(agreementNumber) ? null : agreementNumber.Trim(),
                FileType = string.IsNullOrWhiteSpace(fileType) ? null : fileType.Trim(),
                ValidFrom = NormalizeDate(validFrom),
                ValidTo = NormalizeDate(validTo),
                SupplierId = supplierIdValue,
                Status = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim(),
                UploadTime = now,
                UploaderName = user.Name,
                OriginalName = file.FileName,
                StoredName = storedName
            };

            var fileId = await _fileRepository.CreateFileAsync(record, cancellationToken);
            var saved = await _fileRepository.GetFileByIdAsync(fileId, cancellationToken);
            if (saved == null)
            {
                throw new InvalidOperationException("Failed to save file metadata.");
            }

            var relativePath = BuildRelativePath(filePath);
            AutoUpgradeResult autoUpgrade;
            try
            {
                autoUpgrade = await _upgradeService.TryAutoUpgradeFromFileAsync(
                    supplierIdValue,
                    fileId,
                    record.FileType,
                    user,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-trigger upgrade workflow.");
                autoUpgrade = new AutoUpgradeResult { Triggered = false };
            }

            return StatusCode(201, new
            {
                data = new
                {
                    saved.Id,
                    saved.AgreementNumber,
                    saved.FileType,
                    saved.ValidFrom,
                    saved.ValidTo,
                    saved.SupplierId,
                    saved.Status,
                    saved.UploadTime,
                    saved.UploaderName,
                    saved.OriginalName,
                    saved.StoredName,
                    path = relativePath,
                    workflowTriggered = autoUpgrade.Triggered,
                    applicationId = autoUpgrade.ApplicationId
                }
            });
        }
        catch (Exception ex)
        {
            SafeDelete(filePath);
            _logger.LogError(ex, "Failed to persist file metadata.");
            return StatusCode(500, new { message = "Failed to save file metadata." });
        }
    }

    /// <summary>
    /// 获取指定供应商的文件列表
    /// </summary>
    /// <param name="supplierId">供应商ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件列表</returns>
    /// <response code="200">成功返回文件列表</response>
    /// <response code="400">供应商ID格式无效</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限访问该供应商的文件</response>
    [HttpGet("supplier/{supplierId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListBySupplier(string? supplierId, CancellationToken cancellationToken)
    {
        var trimmed = (supplierId ?? string.Empty).Trim();
        if (!int.TryParse(trimmed, out var supplierIdValue))
        {
            return BadRequest(new { message = "Invalid supplier id." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!CanAccessFiles(user, supplierIdValue))
        {
            return StatusCode(403, new { message = "Access denied to supplier files." });
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);
        var files = await _fileRepository.ListFilesBySupplierAsync(supplierIdValue, cancellationToken);
        return Ok(new { data = files });
    }

    /// <summary>
    /// 获取文件元数据
    /// </summary>
    /// <param name="fileId">文件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件元数据</returns>
    /// <response code="200">成功返回文件元数据</response>
    /// <response code="400">文件ID格式无效</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限访问该文件</response>
    /// <response code="404">文件不存在</response>
    [HttpGet("metadata/{fileId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMetadata(string? fileId, CancellationToken cancellationToken)
    {
        if (!int.TryParse((fileId ?? string.Empty).Trim(), out var fileIdValue))
        {
            return BadRequest(new { message = "Invalid file id." });
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);
        var record = await _fileRepository.GetFileByIdAsync(fileIdValue, cancellationToken);
        if (record == null)
        {
            return NotFound(new { message = "File metadata not found." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        if (!CanAccessFiles(user, record.SupplierId))
        {
            return StatusCode(403, new { message = "Access denied to file metadata." });
        }

        var supplierRoot = UploadPathHelper.GetSupplierFilesRoot(_environment);
        var filePath = Path.Combine(supplierRoot, record.SupplierId.ToString(), record.StoredName ?? string.Empty);
        var relativePath = BuildRelativePath(filePath);

        return Ok(new
        {
            data = new
            {
                record.Id,
                record.AgreementNumber,
                record.FileType,
                record.ValidFrom,
                record.ValidTo,
                record.SupplierId,
                record.Status,
                record.UploadTime,
                record.UploaderName,
                record.OriginalName,
                record.StoredName,
                path = relativePath
            }
        });
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="fileId">文件ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件流</returns>
    /// <response code="200">成功返回文件流</response>
    /// <response code="400">文件ID格式无效</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限下载该文件</response>
    /// <response code="404">文件不存在</response>
    [HttpGet("download/{fileId}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string? fileId, CancellationToken cancellationToken)
    {
        if (!int.TryParse((fileId ?? string.Empty).Trim(), out var fileIdValue))
        {
            return BadRequest(new { message = "Invalid file id." });
        }

        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);
        var record = await _fileRepository.GetFileByIdAsync(fileIdValue, cancellationToken);
        if (record == null)
        {
            return NotFound(new { message = "File metadata not found." });
        }

        if (!CanAccessFiles(user, record.SupplierId))
        {
            return StatusCode(403, new { message = "Access denied to download file." });
        }

        var filePath = await ResolveSupplierFilePathAsync(record.SupplierId, record.StoredName, cancellationToken);
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "File not found on disk." });
        }

        var downloadName = string.IsNullOrWhiteSpace(record.OriginalName)
            ? record.StoredName ?? "download"
            : record.OriginalName;

        if (!ContentTypeProvider.TryGetContentType(downloadName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(filePath, contentType, downloadName);
    }

    /// <summary>
    /// 生成安全的文件下载令牌
    /// </summary>
    /// <param name="body">请求体，包含fileId、expiryMinutes、resourceType</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>下载令牌和URL</returns>
    /// <response code="200">成功生成下载令牌</response>
    /// <response code="400">请求参数错误</response>
    /// <response code="401">未授权访问</response>
    /// <response code="403">无权限访问该文件</response>
    /// <response code="404">文件不存在</response>
    [HttpPost("generate-download-token")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateDownloadToken([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var user = HttpContext.GetAuthUser();
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var fileId = ReadInt(body, "fileId");
        if (!fileId.HasValue || fileId.Value <= 0)
        {
            return BadRequest(new { message = "fileId is required." });
        }

        var expiryMinutes = ReadInt(body, "expiryMinutes") ?? DefaultDownloadTokenMinutes;
        if (expiryMinutes < 1 || expiryMinutes > MaxDownloadTokenMinutes)
        {
            return BadRequest(new
            {
                message = "Invalid expiryMinutes",
                hint = $"Must be a positive number between 1 and {MaxDownloadTokenMinutes}"
            });
        }

        var resourceType = ReadString(body, "resourceType") ?? DefaultResourceType;

        await _migrationService.EnsureMigratedAsync(cancellationToken);
        var record = await _fileRepository.GetFileByIdAsync(fileId.Value, cancellationToken);
        if (record == null)
        {
            return NotFound(new { message = "File not found." });
        }

        if (!CanAccessFiles(user, record.SupplierId))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        var storedName = record.StoredName ?? string.Empty;
        var filePath = await ResolveSupplierFilePathAsync(record.SupplierId, storedName, cancellationToken);
        var storagePath = $"{record.SupplierId}/{storedName}";

        long? fileSize = null;
        string? mimeType = null;
        if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
        {
            try
            {
                var info = new FileInfo(filePath);
                fileSize = info.Length;
                var downloadName = record.OriginalName ?? storedName;
                if (ContentTypeProvider.TryGetContentType(downloadName, out var resolved))
                {
                    mimeType = resolved;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect file for token metadata.");
            }
        }

        var token = CreateAccessToken();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var tokenRecord = new FileAccessTokenRecord(
            token,
            user.Id,
            resourceType,
            fileId.Value.ToString(),
            DefaultTokenCategory,
            storagePath,
            record.OriginalName,
            fileSize,
            mimeType,
            expiresAt);

        await InsertAccessTokenAsync(tokenRecord, ipAddress, cancellationToken);

        return Ok(new
        {
            token,
            downloadUrl = $"/api/files/secure-download?token={token}&type={resourceType}&id={fileId.Value}",
            expiresAt = expiresAt.ToString("o")
        });
    }

    /// <summary>
    /// 使用令牌安全下载文件（允许匿名访问）
    /// </summary>
    /// <param name="token">下载令牌</param>
    /// <param name="type">资源类型</param>
    /// <param name="id">资源ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>文件流</returns>
    /// <response code="200">成功返回文件流</response>
    /// <response code="400">缺少必需参数</response>
    /// <response code="403">令牌无效或已过期</response>
    /// <response code="404">文件不存在</response>
    [AllowAnonymous]
    [HttpGet("secure-download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SecureDownload(
        [FromQuery] string? token,
        [FromQuery] string? type,
        [FromQuery] string? id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { message = "token, type, and id are required" });
        }

        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var record = await ConsumeAccessTokenAsync(token, type, id, ipAddress, cancellationToken);
        if (record == null)
        {
            return StatusCode(403, new { message = "Invalid or expired token" });
        }

        var filePath = await ResolveTokenFilePathAsync(record.StoragePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
        {
            return NotFound(new { message = "File not found on disk." });
        }

        var downloadName = string.IsNullOrWhiteSpace(record.OriginalName) ? "download" : record.OriginalName;
        var contentType = record.MimeType;
        if (string.IsNullOrWhiteSpace(contentType) &&
            !ContentTypeProvider.TryGetContentType(downloadName, out contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(filePath, contentType, downloadName);
    }

    private static string? ValidateUpload(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        if (!FileUploadRules.AllowedExtensions.Contains(extension))
        {
            return "Unsupported file format. Allowed: pdf, doc, docx, xlsx, jpg, png.";
        }

        var mime = file.ContentType ?? string.Empty;
        if (string.Equals(mime, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return "Generic binary files (application/octet-stream) are not allowed for security reasons.";
        }

        if (!FileUploadRules.AllowedMimeTypes.Contains(mime))
        {
            return "Unsupported file type. Allowed: pdf, doc, docx, xlsx, jpg, png.";
        }

        return null;
    }

    private static bool CanUploadFiles(AuthUser user, int supplierId)
    {
        if (HasAnyPermission(user, StaffFilePermissions))
        {
            return true;
        }

        return IsOwner(user, supplierId) && HasAnyPermission(user, SupplierUploadPermissions);
    }

    private static bool CanAccessFiles(AuthUser user, int supplierId)
    {
        if (HasAnyPermission(user, StaffFilePermissions))
        {
            return true;
        }

        return IsOwner(user, supplierId) && HasAnyPermission(user, SupplierAccessPermissions);
    }

    private static bool IsOwner(AuthUser user, int supplierId)
    {
        return user.SupplierId.HasValue && user.SupplierId.Value == supplierId;
    }

    private static bool HasAnyPermission(AuthUser user, IEnumerable<string> permissions)
    {
        if (user == null)
        {
            return false;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        return permissions.Any(granted.Contains);
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

    private string BuildRelativePath(string filePath)
    {
        var uploadsRoot = UploadPathHelper.GetGenericUploadsRoot(_environment);
        var relative = Path.GetRelativePath(uploadsRoot, filePath)
            .Replace(Path.DirectorySeparatorChar, '/');
        return $"/uploads/{relative}";
    }

    private async Task ScanWithClamAvAsync(
        string filePath,
        string originalName,
        CancellationToken cancellationToken)
    {
        if (!IsClamAvEnabled())
        {
            _logger.LogInformation("[FileUpload] ClamAV scanning is disabled. Skipping virus scan.");
            return;
        }

        var scanUrl = _configuration["CLAMAV_SCAN_URL"];
        if (string.IsNullOrWhiteSpace(scanUrl))
        {
            throw new InvalidOperationException("ClamAV scanning service is not configured.");
        }

        var apiKey = _configuration["CLAMAV_API_KEY"];
        var timeout = ResolveClamAvTimeout();

        using var client = new HttpClient { Timeout = timeout };
        using var form = new MultipartFormDataContent();
        await using var stream = System.IO.File.OpenRead(filePath);
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(content, "file", originalName);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        }

        using var response = await client.PostAsync(scanUrl, form, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("ClamAV scan failed.");
        }

        var clean = TryParseClamAvResult(responseBody);
        if (clean == null)
        {
            throw new InvalidOperationException("Unable to determine ClamAV scan result.");
        }

        if (!clean.Value)
        {
            throw new InvalidOperationException("File failed compliance scan.");
        }
    }

    private static bool? TryParseClamAvResult(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryReadBoolean(root, "clean", out var clean))
                {
                    return clean;
                }

                if (TryReadBoolean(root, "malicious", out var malicious))
                {
                    return !malicious;
                }

                if (TryReadBoolean(root, "infected", out var infected))
                {
                    return !infected;
                }

                if (TryReadBoolean(root, "isInfected", out var isInfected))
                {
                    return !isInfected;
                }

                if (root.TryGetProperty("status", out var statusValue) &&
                    statusValue.ValueKind == JsonValueKind.String)
                {
                    var status = statusValue.GetString() ?? string.Empty;
                    if (status.Contains("clean", StringComparison.OrdinalIgnoreCase) ||
                        status.Contains("ok", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    if (status.Contains("infected", StringComparison.OrdinalIgnoreCase) ||
                        status.Contains("virus", StringComparison.OrdinalIgnoreCase) ||
                        status.Contains("fail", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }
        }
        catch
        {
            // fallthrough
        }

        var normalized = responseBody.Trim().ToLowerInvariant();
        if (normalized.Contains("clean") || normalized.Contains("ok"))
        {
            return true;
        }

        if (normalized.Contains("infected") || normalized.Contains("virus"))
        {
            return false;
        }

        return null;
    }

    private static bool TryReadBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String &&
            bool.TryParse(property.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static string? ReadString(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private bool IsClamAvEnabled()
    {
        var enabled = _configuration["CLAMAV_ENABLED"];
        return string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase);
    }

    private TimeSpan ResolveClamAvTimeout()
    {
        var timeoutRaw = _configuration["CLAMAV_TIMEOUT_MS"] ?? _configuration["CLAMAV_TIMEOUT"];
        if (int.TryParse(timeoutRaw, out var timeoutMs) && timeoutMs > 0)
        {
            return TimeSpan.FromMilliseconds(timeoutMs);
        }

        return TimeSpan.FromMilliseconds(20000);
    }

    private static string CreateAccessToken()
    {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    private async Task InsertAccessTokenAsync(
        FileAccessTokenRecord record,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO file_access_tokens (
  token, userId, resourceType, resourceId, category, storagePath,
  originalName, fileSize, mimeType, expiresAt, ipAddress
) VALUES (
  @token, @userId, @resourceType, @resourceId, @category, @storagePath,
  @originalName, @fileSize, @mimeType, @expiresAt, @ipAddress
);";
        AddParameter(command, "@token", record.Token);
        AddParameter(command, "@userId", record.UserId);
        AddParameter(command, "@resourceType", record.ResourceType);
        AddParameter(command, "@resourceId", record.ResourceId);
        AddParameter(command, "@category", record.Category);
        AddParameter(command, "@storagePath", record.StoragePath);
        AddParameter(command, "@originalName", record.OriginalName);
        AddParameter(command, "@fileSize", record.FileSize);
        AddParameter(command, "@mimeType", record.MimeType);
        AddParameter(command, "@expiresAt", record.ExpiresAt.UtcDateTime);
        AddParameter(command, "@ipAddress", ipAddress);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<FileAccessTokenRecord?> ConsumeAccessTokenAsync(
        string token,
        string resourceType,
        string resourceId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);

        await using var select = connection.CreateCommand();
        select.CommandText = @"
SELECT token, userId, resourceType, resourceId, category, storagePath,
       originalName, fileSize, mimeType, expiresAt
FROM file_access_tokens
WHERE token = @token AND resourceType = @resourceType AND resourceId = @resourceId AND usedAt IS NULL;";
        AddParameter(select, "@token", token);
        AddParameter(select, "@resourceType", resourceType);
        AddParameter(select, "@resourceId", resourceId);

        var row = SqlServerHelper.ReadSingle(select);
        if (row == null)
        {
            return null;
        }

        var expiresAt = GetDateTimeOffset(row, "expiresAt");
        if (expiresAt.HasValue && expiresAt.Value < DateTimeOffset.UtcNow)
        {
            return null;
        }

        await using var update = connection.CreateCommand();
        update.CommandText = @"
UPDATE file_access_tokens
SET usedAt = SYSUTCDATETIME(), usedIpAddress = @usedIpAddress
WHERE token = @token AND usedAt IS NULL;";
        AddParameter(update, "@token", token);
        AddParameter(update, "@usedIpAddress", ipAddress);

        var updated = await update.ExecuteNonQueryAsync(cancellationToken);
        if (updated == 0)
        {
            return null;
        }

        return new FileAccessTokenRecord(
            GetString(row, "token") ?? token,
            GetString(row, "userId") ?? string.Empty,
            GetString(row, "resourceType") ?? resourceType,
            GetString(row, "resourceId") ?? resourceId,
            GetString(row, "category") ?? DefaultTokenCategory,
            GetString(row, "storagePath") ?? string.Empty,
            GetString(row, "originalName"),
            GetLong(row, "fileSize"),
            GetString(row, "mimeType"),
            expiresAt ?? DateTimeOffset.UtcNow);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    private static long? GetLong(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            decimal decimalValue => (long)decimalValue,
            _ => long.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private static DateTimeOffset? GetDateTimeOffset(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset offsetValue)
        {
            return offsetValue;
        }

        if (value is DateTime dateValue)
        {
            return new DateTimeOffset(dateValue, TimeSpan.Zero);
        }

        if (DateTimeOffset.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private async Task<string?> ResolveSupplierFilePathAsync(
        int supplierId,
        string? storedName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storedName))
        {
            return null;
        }

        var supplierRoot = UploadPathHelper.GetSupplierFilesRoot(_environment);
        var primaryPath = Path.Combine(supplierRoot, supplierId.ToString(), storedName);
        if (System.IO.File.Exists(primaryPath))
        {
            return primaryPath;
        }

        var legacyRoot = UploadPathHelper.GetTempSupplierDocumentsRoot(_environment);
        var legacyPath = Path.Combine(legacyRoot, storedName);
        if (System.IO.File.Exists(legacyPath))
        {
            return legacyPath;
        }

        var supplierInfo = await _upgradeRepository.GetSupplierAsync(supplierId, cancellationToken);
        var companyName = supplierInfo?.CompanyName;
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return null;
        }

        var namedRoot = UploadPathHelper.GetSupplierDocumentsRootByName(_environment, supplierId, companyName);
        var namedPath = Path.Combine(namedRoot, storedName);
        return System.IO.File.Exists(namedPath) ? namedPath : null;
    }

    private async Task<string?> ResolveTokenFilePathAsync(
        string? storagePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        var supplierRoot = UploadPathHelper.GetSupplierFilesRoot(_environment);
        var normalized = storagePath.Replace('/', Path.DirectorySeparatorChar);
        var primaryPath = Path.Combine(supplierRoot, normalized);
        if (System.IO.File.Exists(primaryPath))
        {
            return primaryPath;
        }

        var storedName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(storedName))
        {
            return null;
        }

        var legacyRoot = UploadPathHelper.GetTempSupplierDocumentsRoot(_environment);
        var legacyPath = Path.Combine(legacyRoot, storedName);
        if (System.IO.File.Exists(legacyPath))
        {
            return legacyPath;
        }

        var supplierId = ParseSupplierIdFromStoragePath(normalized);
        if (!supplierId.HasValue)
        {
            return null;
        }

        var supplierInfo = await _upgradeRepository.GetSupplierAsync(supplierId.Value, cancellationToken);
        var companyName = supplierInfo?.CompanyName;
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return null;
        }

        var namedRoot = UploadPathHelper.GetSupplierDocumentsRootByName(_environment, supplierId.Value, companyName);
        var namedPath = Path.Combine(namedRoot, storedName);
        return System.IO.File.Exists(namedPath) ? namedPath : null;
    }

    private static int? ParseSupplierIdFromStoragePath(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return null;
        }

        var parts = normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        return int.TryParse(parts[0], out var supplierId) ? supplierId : null;
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

    private static readonly HashSet<string> StaffFilePermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorRfqApprove,
        Permissions.ProcurementDirectorReportsView,
        Permissions.FinanceAccountantReconciliation,
        Permissions.FinanceDirectorRiskMonitor,
        Permissions.AdminRoleManage,
        Permissions.AdminSupplierTags
    };

    private static readonly HashSet<string> SupplierAccessPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.SupplierContractChecklist,
        Permissions.SupplierContractUpload
    };

    private static readonly HashSet<string> SupplierUploadPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.SupplierContractUpload
    };

    private sealed record FileAccessTokenRecord(
        string Token,
        string UserId,
        string ResourceType,
        string ResourceId,
        string Category,
        string StoragePath,
        string? OriginalName,
        long? FileSize,
        string? MimeType,
        DateTimeOffset ExpiresAt);
}
