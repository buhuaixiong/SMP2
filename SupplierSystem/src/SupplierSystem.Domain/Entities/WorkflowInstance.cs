namespace SupplierSystem.Domain.Entities;

public sealed class WorkflowInstance
{
    public int Id { get; set; }
    public string? WorkflowType { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Status { get; set; }
    public string? CurrentStep { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
