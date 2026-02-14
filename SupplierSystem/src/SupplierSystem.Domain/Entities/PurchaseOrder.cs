namespace SupplierSystem.Domain.Entities;

public sealed class PurchaseOrder
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = null!;
    public int RfqId { get; set; }
    public int SupplierId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Currency { get; set; }
    public int? ItemCount { get; set; }
    public string? PoFilePath { get; set; }
    public string? PoFileName { get; set; }
    public long? PoFileSize { get; set; }
    public string? Status { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
