namespace SupplierSystem.Domain.Entities;

public sealed class MaterialRequisitionItem
{
    public int Id { get; set; }
    public int RequisitionId { get; set; }
    public string ItemType { get; set; } = null!;
    public string? ItemSubtype { get; set; }
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public string? ItemDescription { get; set; }
    public string? Specifications { get; set; }
    public decimal? EstimatedBudget { get; set; }
    public string? Currency { get; set; }
    public int? ConvertedToRfqId { get; set; }
    public string? ConvertedAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
