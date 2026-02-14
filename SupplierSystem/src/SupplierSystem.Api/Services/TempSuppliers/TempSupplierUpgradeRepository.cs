using System.Data.Common;
using System.Linq;
using SupplierSystem.Api.Services;
using SupplierSystem.Api.Services.Workflows;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.TempSuppliers;

public sealed class TempSupplierUpgradeRepository
{
    private readonly SupplierSystemDbContext _dbContext;

    public TempSupplierUpgradeRepository(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // 邂蛹厄ｼ壻ｽｿ逕ｨ豕帛梛SQL謇ｧ陦悟勣蜃丞ｰ鷹㍾螟堺ｻ｣遐?
    private async Task<T?> ExecuteQueryAsync<T>(
        string sql,
        Func<Dictionary<string, object?>, T> mapper,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters) where T : class
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            AddParameter(command, name, value);
        }

        var row = SqlServerHelper.ReadSingle(command);
        return row == null ? null : mapper(row);
    }

    private async Task<List<T>> ExecuteQueryListAsync<T>(
        string sql,
        Func<Dictionary<string, object?>, T> mapper,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            AddParameter(command, name, value);
        }

        return SqlServerHelper.ReadAll(command).Select(mapper).ToList();
    }

    private async Task<int> ExecuteNonQueryAsync(
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            AddParameter(command, name, value);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
        return 1; // success indicator
    }

    public Task<UpgradeApplicationRecord?> GetLatestApplicationAsync(int supplierId, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync(
            @"SELECT TOP (1) id, supplier_id, status, currentStep, submittedAt, submittedBy, dueAt,
              workflowId, rejectionReason, createdAt, updatedAt
              FROM supplier_upgrade_applications
              WHERE supplier_id = @supplierId ORDER BY createdAt DESC;",
            MapApplication,
            cancellationToken,
            ("@supplierId", supplierId));
    }

    public Task<SupplierSummary?> GetSupplierAsync(int supplierId, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync(
            "SELECT id, companyName, stage, status, currentApprover, contactPerson, contactEmail FROM suppliers WHERE id = @supplierId;",
            MapSupplier,
            cancellationToken,
            ("@supplierId", supplierId));
    }

    public Task<UpgradeApplicationRecord?> GetApplicationAsync(int applicationId, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync(
            @"SELECT id, supplier_id, status, currentStep, submittedAt, submittedBy, dueAt,
              workflowId, rejectionReason, createdAt, updatedAt
              FROM supplier_upgrade_applications WHERE id = @id;",
            MapApplication,
            cancellationToken,
            ("@id", applicationId));
    }

    public async Task<List<PendingUpgradeApplicationRecord>> ListPendingApplicationsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT sua.id, sua.supplier_id, sua.status, sua.currentStep, sua.submittedAt, sua.submittedBy,
       sua.dueAt, sua.workflowId, sua.rejectionReason, sua.createdAt, sua.updatedAt,
       s.companyName, s.contactPerson, s.contactEmail
FROM supplier_upgrade_applications sua
INNER JOIN suppliers s ON s.id = sua.supplier_id
WHERE sua.status LIKE 'pending_%' ORDER BY sua.submittedAt ASC;";
        return SqlServerHelper.ReadAll(command).Select(MapPendingApplication).ToList();
    }

    public async Task<List<PendingUpgradeApplicationRecord>> ListApprovedApplicationsByApproverAsync(
        string approverId,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT sua.id, sua.supplier_id, sua.status, sua.currentStep, sua.submittedAt, sua.submittedBy,
       sua.dueAt, sua.workflowId, sua.rejectionReason, sua.createdAt, sua.updatedAt,
       s.companyName, s.contactPerson, s.contactEmail
FROM supplier_upgrade_applications sua
INNER JOIN (
  SELECT applicationId, MAX(decidedAt) AS decidedAt
  FROM supplier_upgrade_reviews
  WHERE decidedById = @approverId
  GROUP BY applicationId
) r ON r.applicationId = sua.id
INNER JOIN suppliers s ON s.id = sua.supplier_id
ORDER BY r.decidedAt DESC;";
        AddParameter(command, "@approverId", approverId);
        return SqlServerHelper.ReadAll(command).Select(MapPendingApplication).ToList();
    }

    public Task<WorkflowInstanceRecord?> GetWorkflowInstanceAsync(int workflowId, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync(
            "SELECT id, workflowType, entityType, entityId, status, currentStep, createdBy, createdAt, updatedAt FROM workflow_instances WHERE id = @id;",
            MapWorkflowInstance,
            cancellationToken,
            ("@id", workflowId));
    }

    public Task<List<WorkflowStepRecord>> GetWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken)
    {
        return ExecuteQueryListAsync(
            "SELECT id, workflowId, stepOrder, name, assignee, status, dueAt, completedAt, notes FROM workflow_steps WHERE workflowId = @workflowId ORDER BY stepOrder ASC;",
            MapWorkflowStep,
            cancellationToken,
            ("@workflowId", workflowId));
    }

    public async Task<int> CreateApplicationAsync(UpgradeApplicationRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO supplier_upgrade_applications (
  supplier_id, status, currentStep, submittedAt, submittedBy, dueAt, workflowId, rejectionReason, createdAt, updatedAt
) VALUES (
  @supplierId, @status, @currentStep, @submittedAt, @submittedBy, @dueAt, @workflowId, @rejectionReason, @createdAt, @updatedAt
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameters(command,
            ("@supplierId", record.SupplierId),
            ("@status", record.Status),
            ("@currentStep", record.CurrentStep),
            ("@submittedAt", record.SubmittedAt),
            ("@submittedBy", record.SubmittedBy),
            ("@dueAt", record.DueAt),
            ("@workflowId", record.WorkflowId),
            ("@rejectionReason", record.RejectionReason),
            ("@createdAt", record.CreatedAt),
            ("@updatedAt", record.UpdatedAt));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public Task UpdateApplicationStatusAsync(int applicationId, string status, string? currentStep, string? rejectionReason, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            @"UPDATE supplier_upgrade_applications SET status = @status, currentStep = @currentStep, rejectionReason = @rejectionReason, updatedAt = @updatedAt WHERE id = @id;",
            cancellationToken,
            ("@status", status),
            ("@currentStep", currentStep),
            ("@rejectionReason", rejectionReason),
            ("@updatedAt", updatedAt.ToString("o")),
            ("@id", applicationId));
    }

    public Task UpdateApplicationStatusOnlyAsync(int applicationId, string status, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            "UPDATE supplier_upgrade_applications SET status = @status, updatedAt = @updatedAt WHERE id = @id;",
            cancellationToken,
            ("@status", status),
            ("@updatedAt", updatedAt.ToString("o")),
            ("@id", applicationId));
    }

    public async Task<int> CreateDocumentAsync(UpgradeDocumentRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO supplier_upgrade_documents (
  applicationId, requirementCode, requirementName, fileId, uploadedAt, uploadedBy, status, notes
) VALUES (
  @applicationId, @requirementCode, @requirementName, @fileId, @uploadedAt, @uploadedBy, @status, @notes
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameters(command,
            ("@applicationId", record.ApplicationId),
            ("@requirementCode", record.RequirementCode),
            ("@requirementName", record.RequirementName),
            ("@fileId", record.FileId),
            ("@uploadedAt", record.UploadedAt),
            ("@uploadedBy", record.UploadedBy),
            ("@status", record.Status),
            ("@notes", record.Notes));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public Task<UpgradeDocumentDetail?> GetDocumentByIdAsync(int documentId, CancellationToken cancellationToken)
    {
        return ExecuteQueryAsync(
            @"SELECT d.id, d.applicationId, d.requirementCode, d.requirementName, d.fileId,
              d.uploadedAt, d.uploadedBy, d.status, d.notes, f.originalName, f.storedName, f.fileType, f.uploadTime, f.validFrom, f.validTo
              FROM supplier_upgrade_documents d LEFT JOIN files f ON f.id = d.fileId WHERE d.id = @documentId;",
            MapDocumentDetail,
            cancellationToken,
            ("@documentId", documentId));
    }

    public Task<bool> DeleteDocumentAsync(int documentId, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync("DELETE FROM supplier_upgrade_documents WHERE id = @documentId;", cancellationToken, ("@documentId", documentId))
            .ContinueWith(t => t.Result > 0, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    public Task<List<UpgradeDocumentDetail>> GetApplicationDocumentsAsync(int applicationId, CancellationToken cancellationToken)
    {
        return ExecuteQueryListAsync(
            @"SELECT d.id, d.applicationId, d.requirementCode, d.requirementName, d.fileId,
              d.uploadedAt, d.uploadedBy, d.status, d.notes, f.originalName, f.storedName, f.fileType, f.uploadTime, f.validFrom, f.validTo
              FROM supplier_upgrade_documents d LEFT JOIN files f ON f.id = d.fileId WHERE d.applicationId = @applicationId ORDER BY d.uploadedAt ASC;",
            MapDocumentDetail,
            cancellationToken,
            ("@applicationId", applicationId));
    }

    public Task<List<UpgradeReviewRecord>> GetApplicationReviewsAsync(int applicationId, CancellationToken cancellationToken)
    {
        return ExecuteQueryListAsync(
            "SELECT id, applicationId, stepKey, stepName, decision, comments, decidedById, decidedByName, decidedAt FROM supplier_upgrade_reviews WHERE applicationId = @applicationId ORDER BY decidedAt ASC;",
            MapReview,
            cancellationToken,
            ("@applicationId", applicationId));
    }

    public async Task<int> CreateReviewAsync(UpgradeReviewRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO supplier_upgrade_reviews (
  applicationId, stepKey, stepName, decision, comments, decidedById, decidedByName, decidedAt
) VALUES (
  @applicationId, @stepKey, @stepName, @decision, @comments, @decidedById, @decidedByName, @decidedAt
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameters(command,
            ("@applicationId", record.ApplicationId),
            ("@stepKey", record.StepKey),
            ("@stepName", record.StepName),
            ("@decision", record.Decision),
            ("@comments", record.Comments),
            ("@decidedById", record.DecidedById),
            ("@decidedByName", record.DecidedByName),
            ("@decidedAt", record.DecidedAt));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public Task UpdateWorkflowStatusAsync(int workflowId, string status, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            "UPDATE workflow_instances SET status = @status, updatedAt = @updatedAt WHERE id = @id;",
            cancellationToken,
            ("@status", status),
            ("@updatedAt", updatedAt.ToString("o")),
            ("@id", workflowId));
    }

    public Task CancelPendingWorkflowStepsAsync(int workflowId, string status, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            "UPDATE workflow_steps SET status = @status WHERE workflowId = @workflowId AND status IN ('waiting', 'pending');",
            cancellationToken,
            ("@status", status),
            ("@workflowId", workflowId));
    }

    public Task UpdateSupplierStatusAsync(int supplierId, string status, string? currentApprover, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            "UPDATE suppliers SET status = @status, currentApprover = @currentApprover, updatedAt = @updatedAt WHERE id = @supplierId;",
            cancellationToken,
            ("@status", status),
            ("@currentApprover", currentApprover),
            ("@updatedAt", updatedAt.ToString("o")),
            ("@supplierId", supplierId));
    }

    public Task PromoteSupplierAsync(int supplierId, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            "UPDATE suppliers SET stage = @stage, status = @status, currentApprover = NULL, updatedAt = @updatedAt WHERE id = @supplierId;",
            cancellationToken,
            ("@stage", "formal"),
            ("@status", "approved"),
            ("@updatedAt", updatedAt.ToString("o")),
            ("@supplierId", supplierId));
    }

    public Task UpgradeSupplierUsersAsync(int supplierId, CancellationToken cancellationToken)
    {
        return ExecuteNonQueryAsync(
            "UPDATE users SET role = 'formal_supplier' WHERE supplier_id = @supplierId AND role = 'temp_supplier';",
            cancellationToken,
            ("@supplierId", supplierId));
    }

    private static SupplierSummary MapSupplier(Dictionary<string, object?> row)
    {
        return new SupplierSummary
        {
            Id = GetInt(row, "id"),
            CompanyName = GetString(row, "companyName"),
            Stage = GetString(row, "stage"),
            Status = GetString(row, "status"),
            CurrentApprover = GetString(row, "currentApprover"),
            ContactPerson = GetString(row, "contactPerson"),
            ContactEmail = GetString(row, "contactEmail"),
        };
    }

    private static UpgradeApplicationRecord MapApplication(Dictionary<string, object?> row)
    {
        return new UpgradeApplicationRecord
        {
            Id = GetInt(row, "id"),
            SupplierId = GetInt(row, "supplier_id"),
            Status = GetString(row, "status") ?? string.Empty,
            CurrentStep = GetString(row, "currentStep"),
            SubmittedAt = GetString(row, "submittedAt"),
            SubmittedBy = GetString(row, "submittedBy"),
            DueAt = GetString(row, "dueAt"),
            WorkflowId = GetNullableInt(row, "workflowId"),
            RejectionReason = GetString(row, "rejectionReason"),
            CreatedAt = GetString(row, "createdAt"),
            UpdatedAt = GetString(row, "updatedAt"),
        };
    }

    private static PendingUpgradeApplicationRecord MapPendingApplication(Dictionary<string, object?> row)
    {
        return new PendingUpgradeApplicationRecord
        {
            Id = GetInt(row, "id"),
            SupplierId = GetInt(row, "supplier_id"),
            Status = GetString(row, "status") ?? string.Empty,
            CurrentStep = GetString(row, "currentStep"),
            SubmittedAt = GetString(row, "submittedAt"),
            SubmittedBy = GetString(row, "submittedBy"),
            DueAt = GetString(row, "dueAt"),
            WorkflowId = GetNullableInt(row, "workflowId"),
            RejectionReason = GetString(row, "rejectionReason"),
            CreatedAt = GetString(row, "createdAt"),
            UpdatedAt = GetString(row, "updatedAt"),
            SupplierName = GetString(row, "companyName"),
            ContactPerson = GetString(row, "contactPerson"),
            ContactEmail = GetString(row, "contactEmail"),
        };
    }

    private static WorkflowInstanceRecord MapWorkflowInstance(Dictionary<string, object?> row)
    {
        return new WorkflowInstanceRecord(
            GetInt(row, "id"),
            GetString(row, "workflowType") ?? string.Empty,
            GetString(row, "entityType") ?? string.Empty,
            GetString(row, "entityId") ?? string.Empty,
            GetString(row, "status") ?? string.Empty,
            GetString(row, "currentStep"),
            GetString(row, "createdBy"),
            GetString(row, "createdAt"),
            GetString(row, "updatedAt"));
    }

    private static WorkflowStepRecord MapWorkflowStep(Dictionary<string, object?> row)
    {
        return new WorkflowStepRecord(
            GetInt(row, "id"),
            GetInt(row, "workflowId"),
            GetInt(row, "stepOrder"),
            GetString(row, "name") ?? string.Empty,
            GetString(row, "assignee"),
            GetString(row, "status") ?? string.Empty,
            GetString(row, "dueAt"),
            GetString(row, "completedAt"),
            GetString(row, "notes"));
    }

    private static UpgradeDocumentDetail MapDocumentDetail(Dictionary<string, object?> row)
    {
        return new UpgradeDocumentDetail
        {
            Id = GetInt(row, "id"),
            ApplicationId = GetInt(row, "applicationId"),
            RequirementCode = GetString(row, "requirementCode") ?? string.Empty,
            RequirementName = GetString(row, "requirementName") ?? string.Empty,
            FileId = GetNullableInt(row, "fileId"),
            UploadedAt = GetString(row, "uploadedAt"),
            UploadedBy = GetString(row, "uploadedBy"),
            Status = GetString(row, "status"),
            Notes = GetString(row, "notes"),
            OriginalName = GetString(row, "originalName"),
            StoredName = GetString(row, "storedName"),
            FileType = GetString(row, "fileType"),
            UploadTime = GetString(row, "uploadTime"),
            ValidFrom = GetString(row, "validFrom"),
            ValidTo = GetString(row, "validTo"),
        };
    }

    private static UpgradeReviewRecord MapReview(Dictionary<string, object?> row)
    {
        return new UpgradeReviewRecord
        {
            Id = GetInt(row, "id"),
            ApplicationId = GetInt(row, "applicationId"),
            StepKey = GetString(row, "stepKey") ?? string.Empty,
            StepName = GetString(row, "stepName") ?? string.Empty,
            Decision = GetString(row, "decision") ?? string.Empty,
            Comments = GetString(row, "comments"),
            DecidedById = GetString(row, "decidedById"),
            DecidedByName = GetString(row, "decidedByName"),
            DecidedAt = GetString(row, "decidedAt"),
        };
    }

    private static int GetInt(Dictionary<string, object?> row, string key)
    {
        return GetNullableInt(row, key) ?? 0;
    }

    private static int? GetNullableInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : null
        };
    }

    private static string? GetString(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTimeOffset offsetValue)
        {
            return offsetValue.ToString("o");
        }

        if (value is DateTime dateValue)
        {
            return new DateTimeOffset(dateValue).ToString("o");
        }

        return value.ToString();
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddParameters(DbCommand command, params (string Name, object? Value)[] parameters)
    {
        foreach (var (name, value) in parameters)
        {
            AddParameter(command, name, value);
        }
    }
}
