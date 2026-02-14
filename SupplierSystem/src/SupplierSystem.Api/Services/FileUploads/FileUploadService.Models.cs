namespace SupplierSystem.Api.Services.FileUploads;

public sealed class FileUploadServiceException : Exception
{
    public FileUploadServiceException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

internal sealed record FileUploadWorkflowStep(
    string Step,
    string Label,
    string Permission,
    IReadOnlyList<string> Roles,
    int Order);

internal sealed record FileUploadSnapshot(
    int Id,
    int SupplierId,
    int FileId,
    string? FileName,
    string? FileDescription,
    string Status,
    string CurrentStep,
    string? SubmittedBy,
    string? SubmittedAt,
    string? RiskLevel,
    string? ValidFrom,
    string? ValidTo,
    string? CreatedAt,
    string? UpdatedAt);

internal sealed record FileUploadApprovalSnapshot(
    int Id,
    int UploadId,
    string? Step,
    string? StepName,
    string? ApproverId,
    string? ApproverName,
    string? Decision,
    string? Comments,
    string? CreatedAt);
