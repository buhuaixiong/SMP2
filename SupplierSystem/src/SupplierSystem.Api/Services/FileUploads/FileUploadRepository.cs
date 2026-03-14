using System.Data.Common;
using SupplierSystem.Api.Services;
using SupplierSystem.Infrastructure.Data;

namespace SupplierSystem.Api.Services.FileUploads;

public sealed class FileUploadRepository
{
    private readonly SupplierSystemDbContext _dbContext;

    public FileUploadRepository(SupplierSystemDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> CreateFileUploadAsync(FileUploadRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO supplier_file_uploads (
  supplier_id, fileId, fileName, fileDescription, status, currentStep,
  submittedBy, submittedAt, riskLevel, validFrom, validTo, createdAt, updatedAt
) VALUES (
  @supplierId, @fileId, @fileName, @fileDescription, @status, @currentStep,
  @submittedBy, @submittedAt, @riskLevel, @validFrom, @validTo, @createdAt, @updatedAt
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameter(command, "@supplierId", record.SupplierId);
        AddParameter(command, "@fileId", record.FileId);
        AddParameter(command, "@fileName", record.FileName);
        AddParameter(command, "@fileDescription", record.FileDescription);
        AddParameter(command, "@status", record.Status);
        AddParameter(command, "@currentStep", record.CurrentStep);
        AddParameter(command, "@submittedBy", record.SubmittedBy);
        AddParameter(command, "@submittedAt", record.SubmittedAt);
        AddParameter(command, "@riskLevel", record.RiskLevel);
        AddParameter(command, "@validFrom", record.ValidFrom);
        AddParameter(command, "@validTo", record.ValidTo);
        AddParameter(command, "@createdAt", record.CreatedAt);
        AddParameter(command, "@updatedAt", record.UpdatedAt);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public async Task<Dictionary<string, object?>?> GetFileUploadByIdAsync(int uploadId, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM supplier_file_uploads WHERE id = @id";
        AddParameter(command, "@id", uploadId);
        return SqlServerHelper.ReadSingle(command);
    }

    public async Task UpdateFileUploadStatusAsync(
        int uploadId,
        string status,
        string currentStep,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE supplier_file_uploads
SET status = @status, currentStep = @currentStep, updatedAt = @updatedAt
WHERE id = @id;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@currentStep", currentStep);
        AddParameter(command, "@updatedAt", updatedAt);
        AddParameter(command, "@id", uploadId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CreateApprovalRecordAsync(FileUploadApprovalRecord record, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO supplier_file_approvals (
  uploadId, step, stepName, approverId, approverName, decision, comments, createdAt
) VALUES (
  @uploadId, @step, @stepName, @approverId, @approverName, @decision, @comments, @createdAt
);
SELECT CAST(SCOPE_IDENTITY() as int);";
        AddParameter(command, "@uploadId", record.UploadId);
        AddParameter(command, "@step", record.Step);
        AddParameter(command, "@stepName", record.StepName);
        AddParameter(command, "@approverId", record.ApproverId);
        AddParameter(command, "@approverName", record.ApproverName);
        AddParameter(command, "@decision", record.Decision);
        AddParameter(command, "@comments", record.Comments);
        AddParameter(command, "@createdAt", record.CreatedAt);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    public async Task<List<Dictionary<string, object?>>> GetApprovalHistoryAsync(int uploadId, CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM supplier_file_approvals
WHERE uploadId = @uploadId
ORDER BY createdAt ASC;";
        AddParameter(command, "@uploadId", uploadId);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetPendingFileUploadsAsync(
        string currentStep,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var status = $"pending_{currentStep}";
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM supplier_file_uploads
WHERE status = @status AND currentStep = @currentStep
ORDER BY submittedAt ASC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;";
        AddParameter(command, "@status", status);
        AddParameter(command, "@currentStep", currentStep);
        AddParameter(command, "@offset", offset);
        AddParameter(command, "@limit", limit);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetApprovedFileUploadsByApproverAsync(
        string approverId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT u.*
FROM supplier_file_uploads u
INNER JOIN (
  SELECT uploadId, MAX(createdAt) AS approvedAt
  FROM supplier_file_approvals
  WHERE approverId = @approverId
  GROUP BY uploadId
) a ON a.uploadId = u.id
ORDER BY a.approvedAt DESC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;";
        AddParameter(command, "@approverId", approverId);
        AddParameter(command, "@offset", offset);
        AddParameter(command, "@limit", limit);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetFileUploadsBySupplierIdAsync(
        int supplierId,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM supplier_file_uploads
WHERE supplier_id = @supplierId
ORDER BY createdAt DESC
OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY;";
        AddParameter(command, "@supplierId", supplierId);
        AddParameter(command, "@offset", offset);
        AddParameter(command, "@limit", limit);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetExpiringFileUploadsAsync(
        int daysThreshold,
        CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM supplier_file_uploads
WHERE status = 'approved'
  AND validTo IS NOT NULL
  AND validTo >= CAST(GETDATE() as date)
  AND validTo <= DATEADD(day, @daysThreshold, CAST(GETDATE() as date))
ORDER BY validTo ASC;";
        AddParameter(command, "@daysThreshold", daysThreshold);
        return SqlServerHelper.ReadAll(command);
    }

    public async Task<List<Dictionary<string, object?>>> GetExpiredFileUploadsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await SqlServerHelper.OpenConnectionAsync(_dbContext, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT *
FROM supplier_file_uploads
WHERE status = 'approved'
  AND validTo IS NOT NULL
  AND validTo < CAST(GETDATE() as date)
ORDER BY validTo DESC;";
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

public sealed class FileUploadRecord
{
    public int SupplierId { get; set; }
    public int FileId { get; set; }
    public string? FileName { get; set; }
    public string? FileDescription { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public string? SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string? RiskLevel { get; set; }
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class FileUploadApprovalRecord
{
    public int UploadId { get; set; }
    public string Step { get; set; } = string.Empty;
    public string? StepName { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
