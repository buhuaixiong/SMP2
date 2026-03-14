namespace SupplierSystem.Domain.Entities;

public sealed class RfqLineItem
{
    public long Id { get; set; }
    public long RfqId { get; set; }
    public int LineNumber { get; set; }
    public string? MaterialCategory { get; set; }
    public string? Brand { get; set; }
    public string? ItemName { get; set; }
    public string? Specifications { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? EstimatedUnitPrice { get; set; }
    public string? Currency { get; set; }
    public string? Parameters { get; set; }
    public string? Notes { get; set; }
    public string? CreatedAt { get; set; }
    public string? Status { get; set; }
    public string? CurrentApproverRole { get; set; }
    public long? SelectedQuoteId { get; set; }
    public long? PoId { get; set; }
    public string? UpdatedAt { get; set; }
}
