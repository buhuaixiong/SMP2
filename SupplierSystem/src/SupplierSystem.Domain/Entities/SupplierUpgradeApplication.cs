namespace SupplierSystem.Domain.Entities;

public sealed class SupplierUpgradeApplication
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string Status { get; set; } = null!;
    public string? CurrentStep { get; set; }
    public string? SubmittedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public string? DueAt { get; set; }
    public int? WorkflowId { get; set; }
    public string? RejectionReason { get; set; }
    public string? ResubmittedAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
