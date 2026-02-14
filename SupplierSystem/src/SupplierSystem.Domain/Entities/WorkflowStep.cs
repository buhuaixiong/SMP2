namespace SupplierSystem.Domain.Entities;

public sealed class WorkflowStep
{
    public int Id { get; set; }
    public int? WorkflowId { get; set; }
    public int? StepOrder { get; set; }
    public string? Name { get; set; }
    public string? Assignee { get; set; }
    public string? Status { get; set; }
    public string? DueAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? Notes { get; set; }
}
