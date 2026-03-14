namespace SupplierSystem.Domain.Entities;

public sealed class LineItemStatusHistory
{
    public int Id { get; set; }
    public int LineItemId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? ChangedBy { get; set; }
    public string? ChangedAt { get; set; }
    public string? Reason { get; set; }
}
