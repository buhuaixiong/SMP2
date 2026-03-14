using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Application.Models.Auth;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed partial class TempSupplierUpgradeService
{
    private readonly SupplierSystemDbContext _dbContext;
    private readonly CompatibilityMigrationService _migrationService;
    private readonly WorkflowEngine _workflowEngine;
    private readonly TempSupplierUpgradeRepository _repository;

    public TempSupplierUpgradeService(
        SupplierSystemDbContext dbContext,
        CompatibilityMigrationService migrationService,
        WorkflowEngine workflowEngine,
        TempSupplierUpgradeRepository repository)
    {
        _dbContext = dbContext;
        _migrationService = migrationService;
        _workflowEngine = workflowEngine;
        _repository = repository;
    }

    public async Task<UpgradeApplicationRecord> SubmitUpgradeApplicationAsync(int supplierId, AuthUser user, CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);
        EnsureUpgradePermission(supplierId, user);

        var supplier = await _dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == supplierId, cancellationToken)
            ?? throw new TempSupplierUpgradeException(400, "Supplier not found");

        if (!string.Equals(supplier.Stage, "temporary", StringComparison.OrdinalIgnoreCase))
        {
            throw new TempSupplierUpgradeException(400, "Only temporary suppliers can apply for upgrade");
        }

        var latest = await _repository.GetLatestApplicationAsync(supplierId, cancellationToken);
        if (latest != null && latest.Status.StartsWith("pending_", StringComparison.OrdinalIgnoreCase))
        {
            throw new TempSupplierUpgradeException(400, "There is already a pending upgrade application for this supplier");
        }

        var now = DateTimeOffset.UtcNow;
        var stepDueAt = AddWorkingDays(now, 3);
        var applicationDueAt = AddWorkingDays(now, 10);
        var workflow = await _workflowEngine.StartAsync(
            TemporarySupplierUpgradeWorkflow.Definition,
            new WorkflowStartRequest("supplier", supplierId.ToString(), user.Id, now, stepDueAt),
            cancellationToken);

        var firstStepKey = TemporarySupplierUpgradeWorkflow.Definition.Steps[0].Key;
        var application = new UpgradeApplicationRecord
        {
            SupplierId = supplierId,
            Status = TemporarySupplierUpgradeWorkflow.StatusForStep(firstStepKey),
            CurrentStep = firstStepKey,
            SubmittedAt = now.ToString("o"),
            SubmittedBy = user.Id,
            DueAt = applicationDueAt.ToString("o"),
            WorkflowId = workflow.Id,
            CreatedAt = now.ToString("o"),
            UpdatedAt = now.ToString("o"),
        };

        application.Id = await _repository.CreateApplicationAsync(application, cancellationToken);
        return application;
    }

    public async Task<UpgradeDecisionResult> ProcessDecisionAsync(
        int applicationId,
        string stepKey,
        string decision,
        string? comments,
        AuthUser user,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var application = await _repository.GetApplicationAsync(applicationId, cancellationToken)
            ?? throw new TempSupplierUpgradeException(404, "Application not found");

        await EnsureAssignedPurchaserAsync(user, application.SupplierId, cancellationToken);

        var expectedStatus = TemporarySupplierUpgradeWorkflow.StatusForStep(stepKey);
        if (!string.Equals(application.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            throw new TempSupplierUpgradeException(400, "Application is not at this approval step");
        }

        var step = TemporarySupplierUpgradeWorkflow.Definition.GetStep(stepKey)
            ?? throw new TempSupplierUpgradeException(400, "Invalid step key");

        if (!WorkflowAuthorization.HasStepPermission(user, step))
        {
            throw new TempSupplierUpgradeException(403, $"You do not have permission to approve this step ({step.Label})");
        }

        var normalizedDecision = NormalizeDecision(decision)
            ?? throw new TempSupplierUpgradeException(400, "Invalid decision");

        var now = DateTimeOffset.UtcNow;
        var advance = await _workflowEngine.ApplyDecisionAsync(
            TemporarySupplierUpgradeWorkflow.Definition,
            new WorkflowDecisionRequest(application.WorkflowId ?? 0, stepKey, normalizedDecision, comments, now),
            cancellationToken);

        // 记录评审结果
        await _repository.CreateReviewAsync(new UpgradeReviewRecord
        {
            ApplicationId = applicationId,
            StepKey = step.Key,
            StepName = step.Label,
            Decision = normalizedDecision == WorkflowDecision.Approved ? "approve" : "reject",
            Comments = comments,
            DecidedById = user.Id,
            DecidedByName = user.Name,
            DecidedAt = now.ToString("o"),
        }, cancellationToken);

        // 简化：统一状态更新逻辑
        return normalizedDecision == WorkflowDecision.Rejected
            ? await HandleRejectedDecisionAsync(applicationId, application, now, comments, cancellationToken)
            : advance.IsFinal
                ? await HandleApprovedFinalDecisionAsync(application, now, cancellationToken)
                : await HandleAdvanceDecisionAsync(application, advance, now, cancellationToken);
    }

    private async Task<UpgradeDecisionResult> HandleRejectedDecisionAsync(int applicationId, UpgradeApplicationRecord application, DateTimeOffset now, string? comments, CancellationToken cancellationToken)
    {
        await _repository.UpdateApplicationStatusAsync(applicationId, "rejected", null, comments, now, cancellationToken);
        await _repository.UpdateSupplierStatusAsync(application.SupplierId, "rejected", null, now, cancellationToken);
        return new UpgradeDecisionResult { Status = "rejected", CurrentStep = null, NextStep = null, IsFinal = true };
    }

    private async Task<UpgradeDecisionResult> HandleApprovedFinalDecisionAsync(UpgradeApplicationRecord application, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _repository.UpdateApplicationStatusAsync(application.Id, "approved", null, null, now, cancellationToken);
        await _repository.PromoteSupplierAsync(application.SupplierId, now, cancellationToken);
        await _repository.UpgradeSupplierUsersAsync(application.SupplierId, cancellationToken);
        return new UpgradeDecisionResult { Status = "approved", CurrentStep = null, NextStep = null, IsFinal = true };
    }

    private async Task<UpgradeDecisionResult> HandleAdvanceDecisionAsync(UpgradeApplicationRecord application, WorkflowAdvanceResult advance, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nextStatus = TemporarySupplierUpgradeWorkflow.StatusForStep(advance.NextStep ?? string.Empty);
        await _repository.UpdateApplicationStatusAsync(application.Id, nextStatus, advance.NextStep, null, now, cancellationToken);
        await _repository.UpdateSupplierStatusAsync(application.SupplierId, nextStatus, advance.NextStep, now, cancellationToken);
        return new UpgradeDecisionResult { Status = nextStatus, CurrentStep = advance.NextStep, NextStep = advance.NextStep, IsFinal = false };
    }
}
