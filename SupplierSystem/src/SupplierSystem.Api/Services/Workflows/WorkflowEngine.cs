namespace SupplierSystem.Api.Services.Workflows;

public sealed class WorkflowEngine
{
    private readonly CompatibilityMigrationService _migrationService;
    private readonly WorkflowStore _store;

    public WorkflowEngine(CompatibilityMigrationService migrationService, WorkflowStore store)
    {
        _migrationService = migrationService;
        _store = store;
    }

    public async Task<WorkflowInstanceRecord> StartAsync(
        WorkflowDefinition definition,
        WorkflowStartRequest request,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);
        return await _store.CreateWorkflowAsync(definition, request, cancellationToken);
    }

    public async Task<WorkflowAdvanceResult> ApplyDecisionAsync(
        WorkflowDefinition definition,
        WorkflowDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await _migrationService.EnsureMigratedAsync(cancellationToken);

        var step = definition.GetStep(request.CurrentStepKey);
        if (step == null)
        {
            throw new WorkflowException(400, "Invalid workflow step");
        }

        var normalizedDecision = NormalizeDecision(request.Decision);
        if (normalizedDecision == null)
        {
            throw new WorkflowException(400, "Invalid decision");
        }

        var stepOrder = definition.GetStepOrder(request.CurrentStepKey);
        if (stepOrder <= 0)
        {
            throw new WorkflowException(400, "Invalid workflow step");
        }

        var decidedAt = request.DecidedAt ?? DateTimeOffset.UtcNow;

        if (normalizedDecision == WorkflowDecision.Rejected)
        {
            await _store.MarkStepAsync(
                request.WorkflowId,
                stepOrder,
                definition.RejectedStepStatus,
                decidedAt,
                request.Comments,
                cancellationToken);

            await _store.CancelPendingStepsAsync(
                request.WorkflowId,
                stepOrder,
                definition.CancelledStepStatus,
                cancellationToken);

            await _store.UpdateWorkflowInstanceAsync(
                request.WorkflowId,
                definition.RejectedStatus,
                null,
                decidedAt,
                cancellationToken);

            return new WorkflowAdvanceResult(
                definition.RejectedStatus,
                null,
                null,
                true);
        }

        await _store.MarkStepAsync(
            request.WorkflowId,
            stepOrder,
            definition.CompletedStepStatus,
            decidedAt,
            request.Comments,
            cancellationToken);

        var nextStep = definition.GetNextStepKey(request.CurrentStepKey);
        if (nextStep == null)
        {
            await _store.UpdateWorkflowInstanceAsync(
                request.WorkflowId,
                definition.CompletedStatus,
                null,
                decidedAt,
                cancellationToken);

            return new WorkflowAdvanceResult(
                definition.CompletedStatus,
                null,
                null,
                true);
        }

        var nextOrder = definition.GetStepOrder(nextStep);
        if (nextOrder <= 0)
        {
            throw new WorkflowException(400, "Invalid workflow step");
        }

        await _store.ActivateStepAsync(
            request.WorkflowId,
            nextOrder,
            definition.PendingStepStatus,
            cancellationToken);

        await _store.UpdateWorkflowInstanceAsync(
            request.WorkflowId,
            definition.InProgressStatus,
            nextStep,
            decidedAt,
            cancellationToken);

        return new WorkflowAdvanceResult(
            definition.InProgressStatus,
            nextStep,
            nextStep,
            false);
    }

    private static string? NormalizeDecision(string decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            return null;
        }

        if (string.Equals(decision, WorkflowDecision.Approved, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowDecision.Approved;
        }

        if (string.Equals(decision, WorkflowDecision.Rejected, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return WorkflowDecision.Rejected;
        }

        return null;
    }
}

public static class WorkflowDecision
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

public sealed class WorkflowException : Exception
{
    public WorkflowException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
