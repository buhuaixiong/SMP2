namespace SupplierSystem.Domain.Entities;

public sealed class RfqProject
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public string? Status { get; set; }
    public string? DueDate { get; set; }
    public string? LockDate { get; set; }
    public string? CreatedAt { get; set; }
}
