namespace SupplierSystem.Domain.Entities;

public sealed class RfqStatusHistory
{
    public long Id { get; set; }
    public long RfqId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? ChangedBy { get; set; }
    public string? ChangedAt { get; set; }
    public string? Reason { get; set; }
}
