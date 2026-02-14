namespace SupplierSystem.Domain.Entities;

public sealed class RfqApproval
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public int StepOrder { get; set; }
    public string StepName { get; set; } = null!;
    public string ApproverRole { get; set; } = null!;
    public string? ApproverId { get; set; }
    public string? Status { get; set; }
    public string? Decision { get; set; }
    public string? DecidedAt { get; set; }
    public string? CreatedAt { get; set; }
}
