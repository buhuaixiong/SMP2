using System.Data.Common;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SupplierSystem.Api.Services;
using SupplierSystem.Domain.Entities;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.Workflows;

public sealed class WorkflowStore
{
    private readonly SupplierSystemDbContext _dbContext;

    public WorkflowStore(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WorkflowInstanceRecord> CreateWorkflowAsync(
        WorkflowDefinition definition,
        WorkflowStartRequest request,
        CancellationToken cancellationToken)
    {
        var createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
        var updatedAt = createdAt;
        var firstStep = definition.Steps.FirstOrDefault()
            ?? throw new InvalidOperationException("Workflow definition has no steps.");

        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var instanceId = await CreateWorkflowInstanceAsync(connection, transaction, new WorkflowInstanceRecord(
            0,
            definition.WorkflowType,
            request.EntityType,
            request.EntityId,
            definition.InProgressStatus,
            firstStep.Key,
            request.CreatedBy,
            createdAt.ToString("o"),
            updatedAt.ToString("o")
        ), cancellationToken);

        await CreateWorkflowStepsAsync(connection, transaction, instanceId, definition, request, createdAt, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new WorkflowInstanceRecord(
            instanceId,
            definition.WorkflowType,
            request.EntityType,
            request.EntityId,
            definition.InProgressStatus,
            firstStep.Key,
            request.CreatedBy,
            createdAt.ToString("o"),
            updatedAt.ToString("o"));
    }

    public IQueryable<WorkflowInstance> QueryWorkflowInstances()
    {
        return _dbContext.WorkflowInstances.AsNoTracking();
    }

    public Task<WorkflowInstance?> FindWorkflowInstanceAsync(int id, bool asNoTracking, CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkflowInstances.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<WorkflowStep?> FindWorkflowStepAsync(int workflowId, int stepId, CancellationToken cancellationToken)
    {
        return _dbContext.WorkflowSteps
            .FirstOrDefaultAsync(item => item.Id == stepId && item.WorkflowId == workflowId, cancellationToken);
    }

    public Task<List<WorkflowStep>> LoadWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken)
    {
        return _dbContext.WorkflowSteps.AsNoTracking()
            .Where(step => step.WorkflowId == workflowId)
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);
    }

    public Task<List<WorkflowStep>> LoadWorkflowStepsAsync(IReadOnlyCollection<int> workflowIds, CancellationToken cancellationToken)
    {
        return _dbContext.WorkflowSteps.AsNoTracking()
            .Where(step => step.WorkflowId.HasValue && workflowIds.Contains(step.WorkflowId.Value))
            .OrderBy(step => step.StepOrder)
            .ToListAsync(cancellationToken);
    }

    public void AddWorkflowInstance(WorkflowInstance workflow)
    {
        _dbContext.WorkflowInstances.Add(workflow);
    }

    public void AddWorkflowSteps(IEnumerable<WorkflowStep> steps)
    {
        _dbContext.WorkflowSteps.AddRange(steps);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task UpdateWorkflowInstanceAsync(
        int workflowId,
        string status,
        string? currentStep,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE workflow_instances
SET status = @status, currentStep = @currentStep, updatedAt = @updatedAt
WHERE id = @id;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@currentStep", currentStep);
        AddParameter(command, "@updatedAt", updatedAt.ToString("o"));
        AddParameter(command, "@id", workflowId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkStepAsync(
        int workflowId,
        int stepOrder,
        string status,
        DateTimeOffset? completedAt,
        string? notes,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE workflow_steps
SET status = @status, completedAt = @completedAt, notes = @notes
WHERE workflowId = @workflowId AND stepOrder = @stepOrder;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@completedAt", completedAt?.ToString("o"));
        AddParameter(command, "@notes", notes);
        AddParameter(command, "@workflowId", workflowId);
        AddParameter(command, "@stepOrder", stepOrder);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ActivateStepAsync(
        int workflowId,
        int stepOrder,
        string status,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE workflow_steps
SET status = @status
WHERE workflowId = @workflowId AND stepOrder = @stepOrder;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@workflowId", workflowId);
        AddParameter(command, "@stepOrder", stepOrder);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CancelPendingStepsAsync(
        int workflowId,
        int fromOrder,
        string status,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE workflow_steps
SET status = @status
WHERE workflowId = @workflowId
  AND stepOrder > @fromOrder
  AND status IN ('waiting', 'pending');";
        AddParameter(command, "@status", status);
        AddParameter(command, "@workflowId", workflowId);
        AddParameter(command, "@fromOrder", fromOrder);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> CreateWorkflowInstanceAsync(
        DbConnection connection,
        DbTransaction transaction,
        WorkflowInstanceRecord record,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO workflow_instances (
  workflowType, entityType, entityId, status, currentStep, createdBy, createdAt, updatedAt
) VALUES (
  @workflowType, @entityType, @entityId, @status, @currentStep, @createdBy, @createdAt, @updatedAt
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameter(command, "@workflowType", record.WorkflowType);
        AddParameter(command, "@entityType", record.EntityType);
        AddParameter(command, "@entityId", record.EntityId);
        AddParameter(command, "@status", record.Status);
        AddParameter(command, "@currentStep", record.CurrentStep);
        AddParameter(command, "@createdBy", record.CreatedBy);
        AddParameter(command, "@createdAt", record.CreatedAt);
        AddParameter(command, "@updatedAt", record.UpdatedAt);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    private static async Task CreateWorkflowStepsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int workflowId,
        WorkflowDefinition definition,
        WorkflowStartRequest request,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < definition.Steps.Count; index++)
        {
            var step = definition.Steps[index];
            var status = index == 0 ? definition.PendingStepStatus : definition.WaitingStepStatus;
            var dueAt = index == 0 ? request.DueAt : null;
            var metadata = JsonSerializer.Serialize(new { key = step.Key, permission = step.Permission });

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO workflow_steps (
  workflowId, stepOrder, name, assignee, status, dueAt, notes
) VALUES (
  @workflowId, @stepOrder, @name, @assignee, @status, @dueAt, @notes
);";
            AddParameter(command, "@workflowId", workflowId);
            AddParameter(command, "@stepOrder", index + 1);
            AddParameter(command, "@name", step.Label);
            AddParameter(command, "@assignee", step.Permission);
            AddParameter(command, "@status", status);
            AddParameter(command, "@dueAt", dueAt?.ToString("o"));
            AddParameter(command, "@notes", metadata);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
