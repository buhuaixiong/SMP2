using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Services;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/templates")]
public sealed class TemplatesController : NodeControllerBase
{
    private static readonly IReadOnlyList<TemplateDefinition> Definitions =
    [
        new TemplateDefinition("quality_compensation_agreement", "Quality Compensation Agreement", "Quality responsibility, compensation terms, and corrective actions."),
        new TemplateDefinition("incoming_packaging_transport_agreement", "Incoming Packaging Transport Agreement", "Packaging standards, transport method, and receiving requirements."),
        new TemplateDefinition("quality_assurance_agreement", "Quality Assurance Agreement", "Warranty period and quality issue handling process."),
        new TemplateDefinition("quality_kpi_targets", "Quality KPI Targets", "Annual quality KPI targets and assessment requirements."),
        new TemplateDefinition("supplier_handbook_template", "Supplier Handbook Template", "Supplier code of conduct, process guidance, and self-checklist."),
    ];

    private static readonly Dictionary<string, TemplateDefinition> DefinitionMap =
        Definitions.ToDictionary(d => d.Code, d => d, StringComparer.OrdinalIgnoreCase);

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        IWebHostEnvironment environment,
        ILogger<TemplatesController> logger) : base(environment)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveTemplates(CancellationToken cancellationToken)
    {
        var active = await _dbContext.TemplateDocuments
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        var activeMap = active.ToDictionary(t => t.TemplateCode, t => ToRecord(t));
        var payload = Definitions.Select(definition => new
        {
            code = definition.Code,
            name = definition.Name,
            description = definition.Description,
            file = activeMap.TryGetValue(definition.Code, out var record) ? record : null,
        }).ToList();

        return Ok(new { data = payload });
    }

    [HttpGet("history/{code}")]
    public async Task<IActionResult> GetTemplateHistory(string code, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminTemplateManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!DefinitionMap.TryGetValue(code, out var definition))
        {
            return NotFound(new { message = "Unknown template code." });
        }

        var history = await _dbContext.TemplateDocuments
            .AsNoTracking()
            .Where(t => t.TemplateCode == definition.Code)
            .OrderByDescending(t => t.UploadedAt)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            data = new
            {
                definition,
                history = history.Select(ToRecord).ToList(),
            }
        });
    }

    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadTemplate([FromForm] IFormFile? file, [FromForm] string? templateCode, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser(), Permissions.AdminTemplateManage);
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (!DefinitionMap.TryGetValue(templateCode ?? string.Empty, out var definition))
        {
            return BadRequest(new { message = "Unsupported template code." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Template file is required." });
        }

        var uploadDir = ResolveUploadDirectory();
        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
        var storedName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(uploadDir, storedName);
        var originalFileName = DecodeFileName(file.FileName);

        try
        {
            await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var now = DateTimeOffset.UtcNow.ToString("o");
            var actor = HttpContext.GetAuthUser();
            var actorName = actor?.Name ?? actor?.Id ?? "system";

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var activeTemplates = await _dbContext.TemplateDocuments
                .Where(t => t.TemplateCode == definition.Code && t.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var template in activeTemplates)
            {
                template.IsActive = false;
            }

            var document = new TemplateDocument
            {
                TemplateCode = definition.Code,
                TemplateName = definition.Name,
                Description = definition.Description,
                StoredName = storedName,
                OriginalName = originalFileName,
                FileType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = actorName,
                UploadedAt = now,
                IsActive = true,
            };

            _dbContext.TemplateDocuments.Add(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (actor != null)
            {
                await _auditService.LogAsync(new AuditEntry
                {
                    ActorId = actor.Id,
                    ActorName = actor.Name,
                    EntityType = "template_document",
                    EntityId = document.Id.ToString(),
                    Action = "upload",
                    Changes = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        templateCode = definition.Code,
                        templateName = definition.Name,
                    }),
                });
            }

            var active = await _dbContext.TemplateDocuments
                .AsNoTracking()
                .Where(t => t.IsActive)
                .OrderBy(t => t.TemplateName)
                .ToListAsync(cancellationToken);

            var activeMap = active.ToDictionary(t => t.TemplateCode, t => ToRecord(t));
            var payload = Definitions.Select(defn => new
            {
                code = defn.Code,
                name = defn.Name,
                description = defn.Description,
                file = activeMap.TryGetValue(defn.Code, out var record) ? record : null,
            }).ToList();

            return StatusCode(201, new { data = payload });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Templates] Failed to upload template document.");
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
                // ignore cleanup failures
            }

            return StatusCode(500, new { message = "Failed to save template document." });
        }
    }

    private IActionResult? RequirePermission(AuthUser? user, params string[] permissions)
    {
        if (user == null)
        {
            return Unauthorized(new { message = "Authentication required." });
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!permissions.All(granted.Contains))
        {
            return StatusCode(403, new { message = "Access denied." });
        }

        return null;
    }

    private string ResolveUploadDirectory()
    {
        return UploadPathHelper.GetTemplatesRoot(_environment);
    }

    private static string DecodeFileName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name ?? string.Empty;
        }

        try
        {
            var bytes = Encoding.Latin1.GetBytes(name);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return name;
        }
    }

    private static object ToRecord(TemplateDocument document)
    {
        return new
        {
            document.Id,
            document.TemplateCode,
            document.TemplateName,
            document.Description,
            document.StoredName,
            document.OriginalName,
            document.FileType,
            document.FileSize,
            document.UploadedBy,
            document.UploadedAt,
            document.IsActive,
            downloadUrl = $"/uploads/templates/{document.StoredName}",
        };
    }

    private sealed record TemplateDefinition(string Code, string Name, string Description);
}
