using System.Linq;

namespace SupplierSystem.Api.Services.Workflows;

public sealed record WorkflowStepDefinition(
    string Key,
    string Label,
    string Permission,
    IReadOnlyList<string> Roles);

public sealed record WorkflowDefinition(
    string WorkflowType,
    IReadOnlyList<WorkflowStepDefinition> Steps)
{
    public string InProgressStatus { get; init; } = "in_progress";
    public string CompletedStatus { get; init; } = "completed";
    public string RejectedStatus { get; init; } = "rejected";
    public string CancelledStatus { get; init; } = "cancelled";
    public string PendingStepStatus { get; init; } = "pending";
    public string WaitingStepStatus { get; init; } = "waiting";
    public string CompletedStepStatus { get; init; } = "completed";
    public string RejectedStepStatus { get; init; } = "rejected";
    public string CancelledStepStatus { get; init; } = "cancelled";

    public WorkflowStepDefinition? GetStep(string stepKey)
    {
        return Steps.FirstOrDefault(step =>
            string.Equals(step.Key, stepKey, StringComparison.OrdinalIgnoreCase));
    }

    public int GetStepOrder(string stepKey)
    {
        for (var index = 0; index < Steps.Count; index++)
        {
            if (string.Equals(Steps[index].Key, stepKey, StringComparison.OrdinalIgnoreCase))
            {
                return index + 1;
            }
        }

        return -1;
    }

    public string? GetNextStepKey(string currentStepKey)
    {
        for (var index = 0; index < Steps.Count; index++)
        {
            if (!string.Equals(Steps[index].Key, currentStepKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nextIndex = index + 1;
            return nextIndex < Steps.Count ? Steps[nextIndex].Key : null;
        }

        return null;
    }
}

public sealed record WorkflowStartRequest(
    string EntityType,
    string EntityId,
    string? CreatedBy = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? DueAt = null);

public sealed record WorkflowDecisionRequest(
    int WorkflowId,
    string CurrentStepKey,
    string Decision,
    string? Comments = null,
    DateTimeOffset? DecidedAt = null);

public sealed record WorkflowAdvanceResult(
    string Status,
    string? CurrentStep,
    string? NextStep,
    bool IsFinal);

public sealed record WorkflowInstanceRecord(
    int Id,
    string WorkflowType,
    string EntityType,
    string EntityId,
    string Status,
    string? CurrentStep,
    string? CreatedBy,
    string? CreatedAt,
    string? UpdatedAt);

public sealed record WorkflowStepRecord(
    int Id,
    int WorkflowId,
    int StepOrder,
    string Name,
    string? Assignee,
    string Status,
    string? DueAt,
    string? CompletedAt,
    string? Notes);
