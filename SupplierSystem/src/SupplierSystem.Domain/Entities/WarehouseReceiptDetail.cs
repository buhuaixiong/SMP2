namespace SupplierSystem.Domain.Entities;

public sealed class WarehouseReceiptDetail
{
    public int Id { get; set; }
    public int WarehouseReceiptId { get; set; }
    public int LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Specification { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? QualityStatus { get; set; }
    public string? Notes { get; set; }
    public string? CreatedAt { get; set; }
}
