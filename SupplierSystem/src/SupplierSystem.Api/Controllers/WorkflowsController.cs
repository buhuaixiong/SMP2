using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Extensions;
using SupplierSystem.Api.Filters;
using SupplierSystem.Application.Interfaces;
using SupplierSystem.Application.Models.Audit;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Application.Security;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Controllers;

[ApiController]
[Authorize]
[NodeResponse]
[Route("api/workflows")]
public sealed class WorkflowsController : ControllerBase
{
    private static readonly HashSet<string> WorkflowPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        Permissions.PurchaserSegmentManage,
        Permissions.ProcurementManagerRfqReview,
        Permissions.ProcurementDirectorProcessException,
        Permissions.ProcurementDirectorReportsView,
        Permissions.AdminRoleManage
    };

    private readonly SupplierSystemDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<WorkflowsController> _logger;

    public WorkflowsController(
        SupplierSystemDbContext dbContext,
        IAuditService auditService,
        ILogger<WorkflowsController> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> ListWorkflows(
        [FromQuery] string? type,
        [FromQuery] string? entityType,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var query = _dbContext.WorkflowInstances.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(workflow => workflow.WorkflowType == type);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(workflow => workflow.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(workflow => workflow.Status == status);
        }

        var workflows = await query
            .OrderByDescending(workflow => workflow.UpdatedAt)
            .ToListAsync(cancellationToken);

        var payload = await BuildWorkflowPayloadsAsync(workflows, cancellationToken);
        return Ok(new { data = payload });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetWorkflow(int id, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid workflow id." });
        }

        var workflow = await _dbContext.WorkflowInstances.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workflow == null)
        {
            return NotFound(new { message = "Workflow not found." });
        }

        var steps = await _dbContext.WorkflowSteps.AsNoTracking()
            .Where(step => step.WorkflowId == id)
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);

        return Ok(new { data = BuildWorkflowPayload(workflow, steps) });
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkflow([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        var workflowType = ReadString(body, "workflowType", "workflow_type");
        var entityType = ReadString(body, "entityType", "entity_type");
        var entityId = ReadString(body, "entityId", "entity_id");

        if (string.IsNullOrWhiteSpace(workflowType) ||
            string.IsNullOrWhiteSpace(entityType) ||
            string.IsNullOrWhiteSpace(entityId))
        {
            return BadRequest(new { message = "workflowType, entityType and entityId are required." });
        }

        var user = HttpContext.GetAuthUser();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var status = NormalizeString(ReadString(body, "status")) ?? "draft";
        var currentStep = NormalizeString(ReadString(body, "currentStep"));
        var createdBy = NormalizeString(user?.Name) ?? NormalizeString(ReadString(body, "createdBy")) ?? "system";

        var workflow = new WorkflowInstance
        {
            WorkflowType = workflowType.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            Status = status,
            CurrentStep = currentStep,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        var stepsPayload = ReadSteps(body);
        var stepEntities = new List<WorkflowStep>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.WorkflowInstances.Add(workflow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (stepsPayload.Count > 0)
            {
                for (var index = 0; index < stepsPayload.Count; index++)
                {
                    var payload = stepsPayload[index];
                    var stepOrder = payload.StepOrder ?? index + 1;
                    var name = NormalizeString(payload.Name) ?? $"Step {index + 1}";
                    var step = new WorkflowStep
                    {
                        WorkflowId = workflow.Id,
                        StepOrder = stepOrder,
                        Name = name,
                        Assignee = NormalizeString(payload.Assignee),
                        Status = NormalizeString(payload.Status) ?? "pending",
                        DueAt = NormalizeString(payload.DueAt),
                        Notes = NormalizeString(payload.Notes)
                    };
                    stepEntities.Add(step);
                }

                _dbContext.WorkflowSteps.AddRange(stepEntities);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create workflow.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to create workflow." });
        }

        await LogAuditAsync(new AuditEntry
        {
            ActorId = user?.Id,
            ActorName = user?.Name,
            EntityType = "workflow",
            EntityId = workflow.Id.ToString(),
            Action = "create",
            Changes = JsonSerializer.Serialize(new
            {
                workflowType = workflow.WorkflowType,
                entityType = workflow.EntityType,
                entityId = workflow.EntityId,
                status = workflow.Status,
                currentStep = workflow.CurrentStep,
                steps = stepEntities.Select(step => new
                {
                    stepOrder = step.StepOrder,
                    name = step.Name,
                    assignee = step.Assignee,
                    status = step.Status,
                    dueAt = step.DueAt,
                    notes = step.Notes
                })
            })
        });

        return StatusCode(StatusCodes.Status201Created, new { data = BuildWorkflowPayload(workflow, stepEntities) });
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateWorkflow(int id, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0)
        {
            return BadRequest(new { message = "Invalid workflow id." });
        }

        var workflow = await _dbContext.WorkflowInstances.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workflow == null)
        {
            return NotFound(new { message = "Workflow not found." });
        }

        var hasStatus = TryReadString(body, "status", out var status);
        var hasCurrentStep = TryReadString(body, "currentStep", out var currentStep);

        if (!hasStatus && !hasCurrentStep)
        {
            return BadRequest(new { message = "No changes provided." });
        }

        if (hasStatus)
        {
            workflow.Status = NormalizeString(status);
        }

        if (hasCurrentStep)
        {
            workflow.CurrentStep = NormalizeString(currentStep);
        }

        workflow.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = HttpContext.GetAuthUser()?.Id,
            ActorName = HttpContext.GetAuthUser()?.Name,
            EntityType = "workflow",
            EntityId = workflow.Id.ToString(),
            Action = "update",
            Changes = body.GetRawText()
        });

        var steps = await _dbContext.WorkflowSteps.AsNoTracking()
            .Where(step => step.WorkflowId == workflow.Id)
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);

        return Ok(new { data = BuildWorkflowPayload(workflow, steps) });
    }

    [HttpPut("{id:int}/steps/{stepId:int}")]
    public async Task<IActionResult> UpdateWorkflowStep(int id, int stepId, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var permissionResult = RequirePermission(HttpContext.GetAuthUser());
        if (permissionResult != null)
        {
            return permissionResult;
        }

        if (id <= 0 || stepId <= 0)
        {
            return BadRequest(new { message = "Invalid identifier." });
        }

        var step = await _dbContext.WorkflowSteps
            .FirstOrDefaultAsync(item => item.Id == stepId && item.WorkflowId == id, cancellationToken);
        if (step == null)
        {
            return NotFound(new { message = "Workflow step not found." });
        }

        var hasStatus = TryReadString(body, "status", out var status);
        var hasAssignee = TryReadString(body, "assignee", out var assignee);
        var hasNotes = TryReadString(body, "notes", out var notes);
        var hasCompletedAt = TryReadString(body, "completedAt", out var completedAt);
        var hasDueAt = TryReadString(body, "dueAt", out var dueAt);

        if (!hasStatus && !hasAssignee && !hasNotes && !hasCompletedAt && !hasDueAt)
        {
            return BadRequest(new { message = "No changes provided." });
        }

        if (hasStatus)
        {
            step.Status = NormalizeString(status);
        }

        if (hasAssignee)
        {
            step.Assignee = NormalizeString(assignee);
        }

        if (hasNotes)
        {
            step.Notes = NormalizeString(notes);
        }

        if (hasCompletedAt)
        {
            step.CompletedAt = NormalizeString(completedAt);
        }

        if (hasDueAt)
        {
            step.DueAt = NormalizeString(dueAt);
        }

        var workflow = await _dbContext.WorkflowInstances.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (workflow != null)
        {
            workflow.UpdatedAt = DateTimeOffset.UtcNow.ToString("o");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogAuditAsync(new AuditEntry
        {
            ActorId = HttpContext.GetAuthUser()?.Id,
            ActorName = HttpContext.GetAuthUser()?.Name,
            EntityType = "workflow_step",
            EntityId = step.Id.ToString(),
            Action = "update",
            Changes = body.GetRawText()
        });

        var steps = await _dbContext.WorkflowSteps.AsNoTracking()
            .Where(item => item.WorkflowId == id)
            .OrderBy(item => item.StepOrder)
            .ToListAsync(cancellationToken);

        if (workflow == null)
        {
            workflow = await _dbContext.WorkflowInstances.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        }

        if (workflow == null)
        {
            return NotFound(new { message = "Workflow not found." });
        }

        return Ok(new { data = BuildWorkflowPayload(workflow, steps) });
    }

    private static IActionResult? RequirePermission(AuthUser? user)
    {
        if (user == null)
        {
            return new UnauthorizedObjectResult(new { message = "Authentication required." });
        }

        if (string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var granted = new HashSet<string>(user.Permissions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        if (!WorkflowPermissions.Any(granted.Contains))
        {
            return new ObjectResult(new { message = "Access denied." }) { StatusCode = 403 };
        }

        return null;
    }

    private static object BuildWorkflowPayload(WorkflowInstance workflow, IReadOnlyList<WorkflowStep> steps)
    {
        return new
        {
            id = workflow.Id,
            workflowType = workflow.WorkflowType,
            entityType = workflow.EntityType,
            entityId = workflow.EntityId,
            status = workflow.Status,
            currentStep = workflow.CurrentStep,
            createdBy = workflow.CreatedBy,
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt,
            steps = steps.Select(step => new
            {
                id = step.Id,
                workflowId = step.WorkflowId,
                stepOrder = step.StepOrder,
                name = step.Name,
                assignee = step.Assignee,
                status = step.Status,
                dueAt = step.DueAt,
                completedAt = step.CompletedAt,
                notes = step.Notes
            })
        };
    }

    private async Task<List<object>> BuildWorkflowPayloadsAsync(IReadOnlyList<WorkflowInstance> workflows, CancellationToken cancellationToken)
    {
        if (workflows.Count == 0)
        {
            return new List<object>();
        }

        var workflowIds = workflows.Select(workflow => workflow.Id).ToList();
        var steps = await _dbContext.WorkflowSteps.AsNoTracking()
            .Where(step => step.WorkflowId.HasValue && workflowIds.Contains(step.WorkflowId.Value))
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);

        var stepMap = steps
            .Where(step => step.WorkflowId.HasValue)
            .GroupBy(step => step.WorkflowId!.Value)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<WorkflowStep>)group.ToList());

        var results = new List<object>(workflows.Count);
        foreach (var workflow in workflows)
        {
            stepMap.TryGetValue(workflow.Id, out var workflowSteps);
            results.Add(BuildWorkflowPayload(workflow, workflowSteps ?? Array.Empty<WorkflowStep>()));
        }

        return results;
    }

    private static bool TryReadString(JsonElement body, string key, out string? value)
    {
        value = null;
        if (!body.TryGetProperty(key, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString();
            return true;
        }

        value = element.ToString();
        return true;
    }

    private static string? ReadString(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static string? NormalizeString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<WorkflowStepPayload> ReadSteps(JsonElement body)
    {
        if (!body.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
        {
            return new List<WorkflowStepPayload>();
        }

        var steps = new List<WorkflowStepPayload>();
        foreach (var stepElement in stepsElement.EnumerateArray())
        {
            if (stepElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            steps.Add(new WorkflowStepPayload
            {
                StepOrder = ReadInt(stepElement, "stepOrder", "step_order"),
                Name = ReadString(stepElement, "name"),
                Assignee = ReadString(stepElement, "assignee"),
                Status = ReadString(stepElement, "status"),
                DueAt = ReadString(stepElement, "dueAt", "due_at"),
                Notes = ReadString(stepElement, "notes")
            });
        }

        return steps;
    }

    private static int? ReadInt(JsonElement body, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!body.TryGetProperty(key, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numeric))
            {
                return numeric;
            }

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out numeric))
            {
                return numeric;
            }
        }

        return null;
    }

    private async Task LogAuditAsync(AuditEntry entry)
    {
        try
        {
            await _auditService.LogAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write workflow audit entry.");
        }
    }

    private sealed class WorkflowStepPayload
    {
        public int? StepOrder { get; set; }
        public string? Name { get; set; }
        public string? Assignee { get; set; }
        public string? Status { get; set; }
        public string? DueAt { get; set; }
        public string? Notes { get; set; }
    }
}
