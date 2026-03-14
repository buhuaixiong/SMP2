using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Application.Models.Auth;

namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed partial class TempSupplierUpgradeService
{
    public async Task<AutoUpgradeResult> TryAutoUpgradeFromFileAsync(
        int supplierId,
        int fileId,
        string? fileType,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        if (!IsTempSupplierUploader(user, supplierId))
        {
            return new AutoUpgradeResult { Triggered = false };
        }

        var supplier = await _repository.GetSupplierAsync(supplierId, cancellationToken);
        if (supplier == null || !string.Equals(supplier.Stage, "temporary", StringComparison.OrdinalIgnoreCase))
        {
            return new AutoUpgradeResult { Triggered = false };
        }

        var now = DateTimeOffset.UtcNow;
        var latest = await _repository.GetLatestApplicationAsync(supplierId, cancellationToken);
        if (latest != null && IsPendingStatus(latest.Status))
        {
            await _repository.UpdateApplicationStatusOnlyAsync(latest.Id, "cancelled", now, cancellationToken);
            if (latest.WorkflowId.HasValue)
            {
                await _repository.UpdateWorkflowStatusAsync(latest.WorkflowId.Value, "cancelled", now, cancellationToken);
                await _repository.CancelPendingWorkflowStepsAsync(latest.WorkflowId.Value, "cancelled", cancellationToken);
            }
        }

        var dueAt = AddWorkingDays(now, 5);
        var createdBy = string.IsNullOrWhiteSpace(user.Name) ? user.Id : user.Name;
        var workflow = await _workflowEngine.StartAsync(
            TemporarySupplierUpgradeWorkflow.Definition,
            new WorkflowStartRequest("supplier", supplierId.ToString(), createdBy, now, dueAt),
            cancellationToken);

        var firstStepKey = TemporarySupplierUpgradeWorkflow.Definition.Steps[0].Key;
        var application = new UpgradeApplicationRecord
        {
            SupplierId = supplierId,
            Status = TemporarySupplierUpgradeWorkflow.StatusForStep(firstStepKey),
            CurrentStep = firstStepKey,
            SubmittedAt = now.ToString("o"),
            SubmittedBy = createdBy,
            DueAt = dueAt.ToString("o"),
            WorkflowId = workflow.Id,
            CreatedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o"),
        };

        application.Id = await _repository.CreateApplicationAsync(application, cancellationToken);

        var requirementCode = string.IsNullOrWhiteSpace(fileType) ? "uploaded_document" : fileType.Trim();
        var requirementName = string.IsNullOrWhiteSpace(fileType) ? "Uploaded Document" : fileType.Trim();
        await _repository.CreateDocumentAsync(new UpgradeDocumentRecord
        {
            ApplicationId = application.Id,
            RequirementCode = requirementCode,
            RequirementName = requirementName,
            FileId = fileId,
            UploadedAt = now.ToString("o"),
            UploadedBy = createdBy,
            Status = "submitted",
            Notes = null
        }, cancellationToken);

        var firstStep = TemporarySupplierUpgradeWorkflow.Definition.Steps[0];
        await _repository.UpdateSupplierStatusAsync(
            supplierId,
            "under_review",
            firstStep.Label,
            now,
            cancellationToken);

        return new AutoUpgradeResult
        {
            Triggered = true,
            ApplicationId = application.Id
        };
    }

    private static bool IsTempSupplierUploader(AuthUser user, int supplierId)
    {
        return user != null &&
               string.Equals(user.Role, "temp_supplier", StringComparison.OrdinalIgnoreCase) &&
               user.SupplierId.HasValue &&
               user.SupplierId.Value == supplierId;
    }

    private static bool IsPendingStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status.StartsWith("pending_", StringComparison.OrdinalIgnoreCase);
    }
}
