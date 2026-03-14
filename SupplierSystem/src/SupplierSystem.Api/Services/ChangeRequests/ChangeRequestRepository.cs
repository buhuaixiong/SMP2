using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Api.Services;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.ChangeRequests;

public sealed class ChangeRequestRepository
{
    private readonly SupplierSystemDbContext _dbContext;

    public ChangeRequestRepository(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CreateChangeRequestAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO supplier_change_requests
(supplier_id, changeType, payload, submittedBy, submittedAt, updatedAt, status, currentStep, riskLevel, requiresQuality)
VALUES (@supplierId, @changeType, @payload, @submittedBy, @submittedAt, @updatedAt, @status, @currentStep, @riskLevel, @requiresQuality);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameter(command, "@supplierId", record.SupplierId);
        AddParameter(command, "@changeType", record.ChangeType);
        AddParameter(command, "@payload", record.Payload);
        AddParameter(command, "@submittedBy", record.SubmittedBy);
        AddParameter(command, "@submittedAt", record.SubmittedAt);
        AddParameter(command, "@updatedAt", record.UpdatedAt);
        AddParameter(command, "@status", record.Status);
        AddParameter(command, "@currentStep", record.CurrentStep);
        AddParameter(command, "@riskLevel", record.RiskLevel);
        AddParameter(command, "@requiresQuality", record.RequiresQuality);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public async Task<Dictionary<string, object?>?> GetChangeRequestByIdAsync(int requestId, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM supplier_change_requests WHERE id = @id";
        AddParameter(command, "@id", requestId);
        return SqlServerHelper.ReadSingle(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetChangeRequestsBySupplierIdAsync(
        int supplierId,
        string? status,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM supplier_change_requests
WHERE supplier_id = @supplierId
  AND (@status IS NULL OR status = @status)
ORDER BY submittedAt DESC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;";
        AddParameter(command, "@supplierId", supplierId);
        AddParameter(command, "@status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status);
        AddParameter(command, "@offset", offset);
        AddParameter(command, "@limit", limit);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetPendingChangeRequestsAsync(
        string currentStep,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var status = $"pending_{currentStep}";
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT cr.*, s.companyName, s.companyId, s.stage
FROM supplier_change_requests cr
LEFT JOIN suppliers s ON cr.supplier_id = s.id
WHERE cr.status = @status AND cr.currentStep = @currentStep
ORDER BY cr.submittedAt ASC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@currentStep", currentStep);
        AddParameter(command, "@offset", offset);
        AddParameter(command, "@limit", limit);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetApprovedChangeRequestsByApproverAsync(
        string approverId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT cr.*, s.companyName, s.companyId, s.stage, a.decision AS approvalDecision, a.createdAt AS approvedAt
FROM change_request_approvals a
INNER JOIN supplier_change_requests cr ON cr.id = a.requestId
LEFT JOIN suppliers s ON cr.supplier_id = s.id
WHERE a.approverId = @approverId
ORDER BY a.createdAt DESC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;";
        AddParameter(command, "@approverId", approverId);
        AddParameter(command, "@offset", offset);
        AddParameter(command, "@limit", limit);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task UpdateChangeRequestStatusAsync(
        int requestId,
        string status,
        string currentStep,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE supplier_change_requests
SET status = @status, currentStep = @currentStep, updatedAt = @updatedAt
WHERE id = @id;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@currentStep", currentStep);
        AddParameter(command, "@updatedAt", updatedAt);
        AddParameter(command, "@id", requestId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CreateApprovalRecordAsync(ChangeRequestApprovalRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO change_request_approvals
(requestId, step, approverId, approverName, decision, comments, createdAt)
VALUES (@requestId, @step, @approverId, @approverName, @decision, @comments, @createdAt);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameter(command, "@requestId", record.RequestId);
        AddParameter(command, "@step", record.Step);
        AddParameter(command, "@approverId", record.ApproverId);
        AddParameter(command, "@approverName", record.ApproverName);
        AddParameter(command, "@decision", record.Decision);
        AddParameter(command, "@comments", record.Comments);
        AddParameter(command, "@createdAt", record.CreatedAt);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public async Task<List<Dictionary<string, object?>>> GetApprovalHistoryAsync(int requestId, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM change_request_approvals
WHERE requestId = @requestId
ORDER BY createdAt ASC;";
        AddParameter(command, "@requestId", requestId);
        return SqlServerHelper.ReadAll(command);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

public sealed class ChangeRequestRecord
{
    public int SupplierId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string SubmittedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public int RequiresQuality { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ChangeRequestApprovalRecord
{
    public int RequestId { get; set; }
    public string Step { get; set; } = string.Empty;
    public string ApproverId { get; set; } = string.Empty;
    public string? ApproverName { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
