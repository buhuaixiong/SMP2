namespace SupplierSystem.Domain.Entities;

public sealed class ApprovalHistory
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? Step { get; set; }
    public string? Approver { get; set; }
    public string? Result { get; set; }
    public string? Date { get; set; }
    public string? Comments { get; set; }
}
