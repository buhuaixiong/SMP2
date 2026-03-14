namespace SupplierSystem.Domain.Entities;

public sealed class SupplierFileApproval
{
    public int Id { get; set; }
    public int UploadId { get; set; }
    public string Step { get; set; } = null!;
    public string? StepName { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? Decision { get; set; }
    public string? Comments { get; set; }
    public string? CreatedAt { get; set; }
}
