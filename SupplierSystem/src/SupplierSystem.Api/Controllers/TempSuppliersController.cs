using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

[ApiController]
[Authorize]
[NodeResponse]
[Route("api/temp-suppliers")]
public sealed class TempSuppliersController : ControllerBase
{
    private const long UpgradeDocumentLimit = 10 * 1024 * 1024;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly SupplierFileRepository _fileRepository;
    private readonly TempSupplierUpgradeRepository _upgradeRepository;
    private readonly TempSupplierUpgradeService _upgradeService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TempSuppliersController> _logger;

    public TempSuppliersController(
        SupplierSystemDbContext dbContext,
        CompatibilityMigrationService migrationService,
        WorkflowEngine workflowEngine,
        IWebHostEnvironment environment,
        ILogger<TempSuppliersController> logger)
    {
        _migrationService = migrationService;
        _environment = environment;
        _logger = logger;
        _upgradeRepository = new TempSupplierUpgradeRepository(dbContext);
        _fileRepository = new SupplierFileRepository(dbContext);
        _upgradeService = new TempSupplierUpgradeService(dbContext, migrationService, workflowEngine, _upgradeRepository);
    }

    // 简化：统一的认证检查和错误处理模式
    private async Task<IActionResult> WithAuthValidation(Func<AuthUser, Task<IActionResult>> action)
    {
        var user = HttpContext.GetAuthUser();
        return user != null ? await action(user) : NodeError(401, "Authentication required", "AUTH_REQUIRED");
    }

    private IActionResult WithValidation(Func<IActionResult> action, params (string Field, object? Value, string? Error)[] validations)
    {
        foreach (var (field, value, error) in validations)
        {
            if (error != null)
            {
                return NodeError(400, error, "VALIDATION_ERROR", new { field });
            }
        }
        return action();
    }

    [HttpGet("upgrade-requirements")]
    public async Task<IActionResult> GetUpgradeRequirements(CancellationToken cancellationToken)
    {
        return await WithAuthValidation(async user =>
        {
            try
            {
                var requirements = await _upgradeService.GetUpgradeRequirementsAsync(cancellationToken);
                return NodeSuccess(requirements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load upgrade requirements.");
                return NodeError(500, ex.Message ?? "Failed to load upgrade requirements", "INTERNAL_ERROR");
            }
        });
    }

    [HttpGet("upgrade-applications/pending")]
    public async Task<IActionResult> ListPendingApplications(CancellationToken cancellationToken)
    {
        return await WithAuthValidation(async user =>
        {
            try
            {
                var applications = await _upgradeService.ListPendingApplicationsAsync(user, cancellationToken);
                return NodeSuccess(applications);
            }
            catch (TempSupplierUpgradeException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load pending upgrade applications.");
                return NodeError(500, ex.Message ?? "Failed to load pending upgrade applications", "INTERNAL_ERROR");
            }
        });
    }

    [HttpPost("{id:int}/upgrade-application")]
    public async Task<IActionResult> SubmitUpgradeApplication(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return NodeError(400, "Invalid supplier ID", "VALIDATION_ERROR");

        return await WithAuthValidation(async user =>
        {
            try
            {
                var application = await _upgradeService.SubmitUpgradeApplicationAsync(id, user, cancellationToken);
                var workflow = await LoadWorkflowAsync(application.WorkflowId, cancellationToken);
                return NodeSuccess(new { application, workflow }, 201, "Upgrade application submitted successfully");
            }
            catch (TempSupplierUpgradeException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to submit upgrade application.");
                return NodeError(500, ex.Message ?? "Failed to submit upgrade application", "INTERNAL_ERROR");
            }
        });
    }

    [HttpGet("{id:int}/upgrade-status")]
    public async Task<IActionResult> GetUpgradeStatus(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return NodeError(400, "Invalid supplier ID", "VALIDATION_ERROR");

        return await WithAuthValidation(async user =>
        {
            try
            {
                var status = await _upgradeService.GetUpgradeStatusAsync(id, user, cancellationToken);
                return NodeSuccess(status);
            }
            catch (TempSupplierUpgradeException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load upgrade status.");
                return NodeError(500, ex.Message ?? "Failed to load upgrade status", "INTERNAL_ERROR");
            }
        });
    }

    [HttpPost("upgrade-applications/{applicationId:int}/steps/{stepKey}/decision")]
    public async Task<IActionResult> ProcessDecision(
        int applicationId,
        string stepKey,
        [FromBody] UpgradeDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        if (applicationId <= 0) return NodeError(400, "Invalid application ID", "VALIDATION_ERROR");

        var decision = request?.Decision ?? string.Empty;
        if (!IsDecisionValid(decision))
        {
            return NodeError(400, "Invalid decision", "VALIDATION_ERROR", new { field = "decision", value = decision });
        }

        var comments = request?.Comments ?? string.Empty;
        if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 5)
        {
            return NodeError(400, "Comments are required and must be at least 5 characters", "VALIDATION_ERROR", new { field = "comments", minLength = 5 });
        }

        return await WithAuthValidation(async user =>
        {
            try
            {
                await _upgradeService.ProcessDecisionAsync(applicationId, stepKey, decision, comments, user, cancellationToken);
                var normalized = DecisionExtensions.NormalizeDecision(decision);
                var responseDecision = normalized == WorkflowDecision.Approved ? "approve" : "reject";

                return NodeSuccess(new
                {
                    success = true,
                    message = $"Application {responseDecision}d successfully",
                    applicationId,
                    stepKey,
                    decision = responseDecision
                }, 200, $"Application {responseDecision}d successfully");
            }
            catch (TempSupplierUpgradeException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (WorkflowException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process upgrade decision.");
                return NodeError(500, ex.Message ?? "Failed to process upgrade decision", "INTERNAL_ERROR");
            }
        });
    }

    [HttpPost("{id:int}/upgrade-application-documents")]
    public async Task<IActionResult> UploadUpgradeDocument(
        int id,
        [FromForm] IFormFile? file,
        [FromForm] string? requirementCode,
        [FromForm] string? code,
        [FromForm] string? documentType,
        [FromForm] string? notes,
        [FromForm] string? validFrom,
        [FromForm] string? validTo,
        CancellationToken cancellationToken)
    {
        if (id <= 0) return NodeError(400, "Invalid supplier ID", "VALIDATION_ERROR");
        if (file == null) return NodeError(400, "No file uploaded", "VALIDATION_ERROR");

        var requirement = ResolveRequirementCode(requirementCode, code, documentType);
        if (string.IsNullOrWhiteSpace(requirement))
        {
            return NodeError(400, "Invalid document type", "VALIDATION_ERROR");
        }

        if (!IsRequirementSupported(requirement))
        {
            return NodeError(400, "Invalid document type", "VALIDATION_ERROR", new
            {
                field = "requirementCode",
                value = requirement,
                validTypes = TempSupplierUpgradeService.RequiredDocuments.Select(item => item.Code).ToList()
            });
        }

        if (file.Length > UpgradeDocumentLimit)
        {
            return StatusCode(400, new { success = false, error = "File too large", message = "File size must not exceed 10MB" });
        }

        if (ValidateFileType(file) is { } fileError)
        {
            return StatusCode(400, new { success = false, error = "Invalid file type", message = fileError });
        }

        return await WithAuthValidation(async user =>
        {
            if (!CanManageUpgrade(user, id))
            {
                return NodeError(403, "You do not have permission to manage this supplier upgrade", "PERMISSION_DENIED");
            }

            string? filePath = null;
            try
            {
                await _migrationService.EnsureMigratedAsync(cancellationToken);
                var application = await _upgradeRepository.GetLatestApplicationAsync(id, cancellationToken);
                if (application == null)
                {
                    return NodeError(400, "No upgrade application found for this supplier", "VALIDATION_ERROR");
                }


                var requirementName = ResolveRequirementName(requirement);
                var storedName = FileUploadRules.BuildRandomStoredName(file.FileName);
                var supplierRoot = UploadPathHelper.GetSupplierFilesRoot(_environment);
                var supplierDir = Path.Combine(supplierRoot, id.ToString());
                Directory.CreateDirectory(supplierDir);
                filePath = Path.Combine(supplierDir, storedName);

                await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var now = DateTimeOffset.UtcNow.ToString("o");
                var fileRecord = new SupplierFileRecord
                {
                    SupplierId = id,
                    OriginalName = file.FileName,
                    StoredName = storedName,
                    FileType = file.ContentType,
                    ValidFrom = NormalizeDate(validFrom),
                    ValidTo = NormalizeDate(validTo),
                    UploadTime = now,
                    UploaderName = user.Name,
                    Status = "active"
                };

                var fileId = await _fileRepository.CreateFileAsync(fileRecord, cancellationToken);
                var document = new UpgradeDocumentRecord
                {
                    ApplicationId = application.Id,
                    RequirementCode = requirement!,
                    RequirementName = requirementName,
                    FileId = fileId,
                    UploadedAt = now,
                    UploadedBy = user.Id,
                    Status = "uploaded",
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes
                };
                document.Id = await _upgradeRepository.CreateDocumentAsync(document, cancellationToken);

                return NodeSuccess(document, 201, "Document uploaded successfully");
            }
            catch (TempSupplierUpgradeException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload upgrade document.");
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    TryDelete(filePath);
                }
                return NodeError(500, ex.Message ?? "Failed to upload document", "INTERNAL_ERROR");
            }
        });
    }

    [HttpGet("upgrade-applications/approved")]
    public async Task<IActionResult> ListApprovedApplications(CancellationToken cancellationToken)
    {
        return await WithAuthValidation(async user =>
        {
            try
            {
                var applications = await _upgradeService.ListApprovedApplicationsAsync(user, cancellationToken);
                return NodeSuccess(applications);
            }
            catch (TempSupplierUpgradeException ex)
            {
                return NodeError(ex.StatusCode, ex.Message, MapErrorCode(ex.StatusCode));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load approved upgrade applications.");
                return NodeError(500, ex.Message ?? "Failed to load approved upgrade applications", "INTERNAL_ERROR");
            }
        });
    }

    [HttpDelete("{id:int}/upgrade-application-documents/{documentId:int}")]
    public async Task<IActionResult> DeleteUpgradeDocument(int id, int documentId, CancellationToken cancellationToken)
    {
        if (id <= 0 || documentId <= 0) return NodeError(400, "Invalid supplier or document ID", "VALIDATION_ERROR");

        return await WithAuthValidation(async user =>
        {
            if (!CanManageUpgrade(user, id))
            {
                return NodeError(403, "You do not have permission to manage this supplier upgrade", "PERMISSION_DENIED");
            }

            try
            {
                await _migrationService.EnsureMigratedAsync(cancellationToken);
                var document = await _upgradeRepository.GetDocumentByIdAsync(documentId, cancellationToken);
                if (document == null)
                {
                    return NodeError(404, "Document not found", "NOT_FOUND");
                }

                var application = await _upgradeRepository.GetApplicationAsync(document.ApplicationId, cancellationToken);
                if (application == null || application.SupplierId != id)
                {
                    return NodeError(404, "Document not found", "NOT_FOUND");
                }

                var storedName = document.StoredName;
                if (string.IsNullOrWhiteSpace(storedName) && document.FileId.HasValue)
                {
                    var fileRecord = await _fileRepository.GetFileByIdAsync(document.FileId.Value, cancellationToken);
                    storedName = fileRecord?.StoredName;
                }

                var deleted = await _upgradeRepository.DeleteDocumentAsync(documentId, cancellationToken);
                if (!deleted)
                {
                    return NodeError(404, "Document not found", "NOT_FOUND");
                }

                if (document.FileId.HasValue)
                {
                    var removed = await _fileRepository.DeleteFileAsync(document.FileId.Value, cancellationToken);
                    if (!removed)
                    {
                        _logger.LogWarning("Upgrade document file metadata already removed: {FileId}", document.FileId.Value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(storedName))
                {
                    // 获取供应商信息用于构建正确的文件路径
                    var supplierInfo = await _upgradeRepository.GetSupplierAsync(id, cancellationToken);
                    var companyName = supplierInfo?.CompanyName ?? $"Supplier_{id}";
                    var uploadRoot = UploadPathHelper.GetSupplierDocumentsRootByName(_environment, id, companyName);
                    var filePath = Path.Combine(uploadRoot, storedName);
                    TryDelete(filePath);
                }

                return NodeSuccess(new { id = documentId }, 200, "Document deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete upgrade document.");
                return NodeError(500, ex.Message ?? "Failed to delete document", "INTERNAL_ERROR");
            }
        });
    }

    private async Task<WorkflowInstanceView?> LoadWorkflowAsync(int? workflowId, CancellationToken cancellationToken)
    {
        if (!workflowId.HasValue) return null;
        var workflow = await _upgradeRepository.GetWorkflowInstanceAsync(workflowId.Value, cancellationToken);
        var steps = await _upgradeRepository.GetWorkflowStepsAsync(workflowId.Value, cancellationToken);
        return TempSupplierUpgradeService.BuildWorkflowView(workflow, steps);
    }

    private static string? ResolveRequirementCode(string? requirementCode, string? code, string? documentType) =>
        !string.IsNullOrWhiteSpace(requirementCode) ? requirementCode :
        !string.IsNullOrWhiteSpace(code) ? code :
        !string.IsNullOrWhiteSpace(documentType) ? documentType : null;

    private static string ResolveRequirementName(string requirementCode) =>
        TempSupplierUpgradeService.RequiredDocuments.FirstOrDefault(item => string.Equals(item.Code, requirementCode, StringComparison.OrdinalIgnoreCase))?.Name ?? requirementCode;

    private static bool IsRequirementSupported(string requirementCode) =>
        TempSupplierUpgradeService.RequiredDocuments.Any(item => string.Equals(item.Code, requirementCode, StringComparison.OrdinalIgnoreCase));

    private static string? ValidateFileType(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        if (!FileUploadRules.AllowedExtensions.Contains(extension) && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return $"Invalid file type: {file.ContentType}. Allowed types: PDF, Word, Excel, JPEG, PNG";
        }
        if (!FileUploadRules.AllowedMimeTypes.Contains(file.ContentType) && !string.Equals(file.ContentType, "image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return $"Invalid file type: {file.ContentType}. Allowed types: PDF, Word, Excel, JPEG, PNG";
        }
        return null;
    }

    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.DateTime.ToString("yyyy-MM-ddTHH:mm:ss");
        }
        return null;
    }

    private static bool IsDecisionValid(string decision)
    {
        if (string.IsNullOrWhiteSpace(decision)) return false;
        var normalized = decision.Trim().ToLowerInvariant();
        return normalized is "approve" or "reject" or "approved" or "rejected";
    }

    private static bool CanManageUpgrade(AuthUser user, int supplierId)
    {
        if (user == null) return false;
        if (user.Permissions?.Contains(Permissions.PurchaserUpgradeInit) == true) return true;
        return user.SupplierId.HasValue && user.SupplierId.Value == supplierId;
    }

    private static IActionResult NodeSuccess(object data, int statusCode = 200, string? message = null)
    {
        var response = new Dictionary<string, object?> { ["success"] = true, ["data"] = data };
        if (!string.IsNullOrWhiteSpace(message)) response["message"] = message;
        return new ObjectResult(response) { StatusCode = statusCode };
    }

    private static IActionResult NodeError(int statusCode, string message, string? code = null, object? details = null)
    {
        var response = new Dictionary<string, object?> { ["success"] = false, ["error"] = message };
        if (!string.IsNullOrWhiteSpace(code)) response["code"] = code;
        if (details != null) response["details"] = details;
        return new ObjectResult(response) { StatusCode = statusCode };
    }

    private static string MapErrorCode(int statusCode) => statusCode switch
    {
        401 => "AUTH_REQUIRED",
        403 => "PERMISSION_DENIED",
        404 => "NOT_FOUND",
        409 => "CONFLICT",
        _ => "VALIDATION_ERROR"
    };

    private static void TryDelete(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        catch { /* best effort cleanup */ }
    }

    public sealed class UpgradeDecisionRequest
    {
        public string? Decision { get; set; }
        public string? Comments { get; set; }
    }
}
