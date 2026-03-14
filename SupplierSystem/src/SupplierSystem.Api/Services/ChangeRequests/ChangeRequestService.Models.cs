namespace SupplierSystem.Api.Services.ChangeRequests;

public sealed class ChangeRequestServiceException : Exception
{
    public ChangeRequestServiceException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

internal sealed record ChangeRequestWorkflowStep(
    string Step,
    string Label,
    string Permission,
    IReadOnlyList<string> Roles);

internal sealed record ChangeRequestFieldDefinition(string Key, string Label);

internal sealed record ChangeRequestField(string Key, string Label, string? NewValue);

internal sealed record ChangeRequestSnapshot(
    int Id,
    int SupplierId,
    string ChangeType,
    string Status,
    string CurrentStep,
    Dictionary<string, string?> Payload,
    string? SubmittedBy,
    string? SubmittedAt,
    string? UpdatedAt,
    string? RiskLevel,
    int? RequiresQuality,
    string? CompanyName,
    string? CompanyId,
    string? Stage);
