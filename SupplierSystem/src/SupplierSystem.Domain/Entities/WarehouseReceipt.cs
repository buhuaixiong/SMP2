namespace SupplierSystem.Domain.Entities;

public sealed class WarehouseReceipt
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public int SupplierId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string ReceiptDate { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Currency { get; set; }
    public string? WarehouseLocation { get; set; }
    public string? ReceiverName { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
