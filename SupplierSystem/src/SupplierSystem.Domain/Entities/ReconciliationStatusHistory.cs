namespace SupplierSystem.Domain.Entities;

public sealed class ReconciliationStatusHistory
{
    public int Id { get; set; }
    public int ReconciliationId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public int? ChangedBy { get; set; }
    public string? ChangedAt { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}
