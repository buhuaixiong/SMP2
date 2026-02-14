namespace SupplierSystem.Api.Models.ChangeRequests;

public sealed class ChangeRequestSubmission
{
    public int? SupplierId { get; set; }
    public Dictionary<string, object?>? Changes { get; set; }
}

public sealed class ChangeRequestApprovalRequest
{
    public string? Decision { get; set; }
    public string? Comments { get; set; }
}
