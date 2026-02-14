using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed partial class TempSupplierUpgradeService
{
    public async Task<object> GetUpgradeStatusAsync(int supplierId, AuthUser user, CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);
        await EnsureViewPermissionAsync(supplierId, user, cancellationToken);

        var application = await _repository.GetLatestApplicationAsync(supplierId, cancellationToken);
        if (application == null)
        {
            return new { hasApplication = false, message = "No upgrade application found for this supplier" };
        }

        WorkflowInstanceRecord? workflow = null;
        var steps = new List<WorkflowStepRecord>();
        if (application.WorkflowId.HasValue)
        {
            workflow = await _repository.GetWorkflowInstanceAsync(application.WorkflowId.Value, cancellationToken);
            steps = await _repository.GetWorkflowStepsAsync(application.WorkflowId.Value, cancellationToken);
        }

        var documents = await _repository.GetApplicationDocumentsAsync(application.Id, cancellationToken);
        var reviews = await _repository.GetApplicationReviewsAsync(application.Id, cancellationToken);
        var templateMap = await BuildTemplateMapAsync(cancellationToken);

        return new
        {
            hasApplication = true,
            application,
            workflow = BuildWorkflowView(workflow, steps),
            documents = documents.Select(BuildDocumentView).ToList(),
            reviews = reviews.Select(BuildReviewView).ToList(),
            requirements = BuildRequirementStatus(documents, templateMap)
        };
    }

    public async Task<IReadOnlyList<object>> ListPendingApplicationsAsync(AuthUser user, CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var approvalPermissions = TemporarySupplierUpgradeWorkflow.Definition.Steps.Select(step => step.Permission);
        if (!HasAnyPermission(user, approvalPermissions))
        {
            throw new TempSupplierUpgradeException(403, "You do not have permission to view upgrade applications");
        }

        var applications = await _repository.ListPendingApplicationsAsync(cancellationToken);
        if (IsPurchaser(user))
        {
            applications = await FilterAssignedSuppliersAsync(applications, user, cancellationToken);
        }

        var result = new List<object>(applications.Count);
        foreach (var app in applications)
        {
            var documents = await _repository.GetApplicationDocumentsAsync(app.Id, cancellationToken);
            result.Add(new
            {
                id = app.Id,
                supplierId = app.SupplierId,
                supplierName = app.SupplierName,
                companyName = app.SupplierName,
                contactPerson = app.ContactPerson,
                contactEmail = app.ContactEmail,
                status = app.Status,
                currentStep = app.CurrentStep,
                submittedAt = app.SubmittedAt,
                submittedBy = app.SubmittedBy,
                dueAt = app.DueAt,
                documentCompleteness = CalculateCompleteness(documents),
                documents = documents.Select(BuildDocumentView).ToList()
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<object>> ListApprovedApplicationsAsync(AuthUser user, CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var approvalPermissions = TemporarySupplierUpgradeWorkflow.Definition.Steps.Select(step => step.Permission);
        if (!HasAnyPermission(user, approvalPermissions))
        {
            throw new TempSupplierUpgradeException(403, "You do not have permission to view upgrade applications");
        }

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return Array.Empty<object>();
        }

        var applications = await _repository.ListApprovedApplicationsByApproverAsync(user.Id, cancellationToken);
        if (IsPurchaser(user))
        {
            applications = await FilterAssignedSuppliersAsync(applications, user, cancellationToken);
        }

        var result = new List<object>(applications.Count);
        foreach (var app in applications)
        {
            var documents = await _repository.GetApplicationDocumentsAsync(app.Id, cancellationToken);
            result.Add(new
            {
                id = app.Id,
                supplierId = app.SupplierId,
                supplierName = app.SupplierName,
                companyName = app.SupplierName,
                contactPerson = app.ContactPerson,
                contactEmail = app.ContactEmail,
                status = app.Status,
                currentStep = app.CurrentStep,
                submittedAt = app.SubmittedAt,
                submittedBy = app.SubmittedBy,
                dueAt = app.DueAt,
                documentCompleteness = CalculateCompleteness(documents),
                documents = documents.Select(BuildDocumentView).ToList()
            });
        }

        return result;
    }

    private async Task<List<PendingUpgradeApplicationRecord>> FilterAssignedSuppliersAsync(
        List<PendingUpgradeApplicationRecord> applications,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        var assignedSupplierIds = await _dbContext.BuyerSupplierAssignments
            .AsNoTracking()
            .Where(a => a.BuyerId == user.Id)
            .Select(a => a.SupplierId)
            .ToListAsync(cancellationToken);

        var groupIds = GetPurchasingGroupIds(user);
        if (groupIds.Count > 0)
        {
            var groupSupplierIds = await _dbContext.PurchasingGroupSuppliers
                .AsNoTracking()
                .Where(a => groupIds.Contains(a.GroupId))
                .Select(a => a.SupplierId)
                .ToListAsync(cancellationToken);
            assignedSupplierIds.AddRange(groupSupplierIds);
        }

        if (assignedSupplierIds.Count == 0)
        {
            return new List<PendingUpgradeApplicationRecord>();
        }

        var assigned = new HashSet<int>(assignedSupplierIds);
        return applications.Where(a => assigned.Contains(a.SupplierId)).ToList();
    }

    // 简化：合并视图构建方法
    private static object BuildDocumentView(UpgradeDocumentDetail document)
    {
        var file = document.FileId.HasValue
            ? new { id = document.FileId.Value, originalName = document.OriginalName, storedName = document.StoredName, fileType = document.FileType, uploadTime = document.UploadTime, validFrom = document.ValidFrom, validTo = document.ValidTo }
            : null;

        return new { id = document.Id, requirementCode = document.RequirementCode, requirementName = document.RequirementName, status = document.Status, notes = document.Notes, uploadedAt = document.UploadedAt, uploadedBy = document.UploadedBy, file };
    }

    private static object BuildReviewView(UpgradeReviewRecord review)
    {
        return new
        {
            id = review.Id,
            applicationId = review.ApplicationId,
            stepKey = review.StepKey,
            stepName = review.StepName,
            decision = review.Decision,
            comments = review.Comments,
            decidedById = review.DecidedById,
            decidedByName = review.DecidedByName,
            decidedAt = NormalizeIsoString(review.DecidedAt),
            source = "upgrade_application"
        };
    }

    private static List<UpgradeRequirementItem> BuildRequirementStatus(IReadOnlyList<UpgradeDocumentDetail> documents, IReadOnlyDictionary<string, object> templateMap)
    {
        var byCode = documents.GroupBy(doc => doc.RequirementCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return RequiredDocuments.Select(item =>
        {
            byCode.TryGetValue(item.Code, out var matched);
            templateMap.TryGetValue(item.Code, out var template);
            return new UpgradeRequirementItem { Code = item.Code, Name = item.Name, Description = item.Description, Required = item.Required, Fulfilled = matched != null, DocumentId = matched?.Id, Template = template };
        }).ToList();
    }

    private static int CalculateCompleteness(IReadOnlyList<UpgradeDocumentDetail> documents)
    {
        var required = RequiredDocuments.Count(item => item.Required);
        if (required == 0) return 0;

        var fulfilled = documents.Count(doc =>
            doc.FileId.HasValue &&
            !string.IsNullOrWhiteSpace(doc.RequirementCode) &&
            RequiredDocuments.Any(req => req.Required && string.Equals(req.Code, doc.RequirementCode, StringComparison.OrdinalIgnoreCase)));

        return (int)Math.Round((double)fulfilled / required * 100, MidpointRounding.AwayFromZero);
    }

    private static WorkflowMetadata ParseWorkflowMetadata(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return new WorkflowMetadata();

        try
        {
            using var document = JsonDocument.Parse(notes);
            var root = document.RootElement;
            return new WorkflowMetadata
            {
                Key = root.TryGetProperty("key", out var k) ? k.GetString() : null,
                Permission = root.TryGetProperty("permission", out var p) ? p.GetString() : null,
                Comment = root.TryGetProperty("comment", out var c) ? c.GetString() : null
            };
        }
        catch { return new WorkflowMetadata(); }
    }

    private static string? NormalizeIsoString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return DateTimeOffset.TryParse(input, out var value) ? value.ToString("o") : input;
    }

    // 公开的工作流视图构建方法（供Controller使用）
    internal static WorkflowInstanceView? BuildWorkflowView(WorkflowInstanceRecord? workflow, IReadOnlyList<WorkflowStepRecord> steps)
    {
        if (workflow == null) return null;
        return new WorkflowInstanceView
        {
            Id = workflow.Id,
            WorkflowType = workflow.WorkflowType,
            EntityType = workflow.EntityType,
            EntityId = workflow.EntityId,
            Status = workflow.Status,
            CurrentStep = workflow.CurrentStep,
            CreatedBy = workflow.CreatedBy,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            Steps = steps.Select(BuildWorkflowStepView).ToList()
        };
    }

    private static UpgradeWorkflowStepView BuildWorkflowStepView(WorkflowStepRecord row)
    {
        var metadata = ParseWorkflowMetadata(row.Notes);
        return new UpgradeWorkflowStepView
        {
            Id = row.Id,
            Order = row.StepOrder,
            Name = row.Name,
            Key = metadata.Key ?? row.Assignee ?? $"step_{row.StepOrder}",
            Permission = metadata.Permission ?? row.Assignee,
            Status = row.Status,
            DueAt = row.DueAt,
            CompletedAt = row.CompletedAt,
            Notes = metadata.Comment
        };
    }

    private sealed class WorkflowMetadata
    {
        public string? Key { get; set; }
        public string? Permission { get; set; }
        public string? Comment { get; set; }
    }
}
