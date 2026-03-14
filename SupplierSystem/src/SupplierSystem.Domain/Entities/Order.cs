namespace SupplierSystem.Domain.Entities;

public sealed class Order
{
    public int Id { get; set; }
    public string? OrderNumber { get; set; }
    public int? RfqId { get; set; }
    public int? SupplierId { get; set; }
    public decimal? ActualAmount { get; set; }
    public string? Status { get; set; }
    public string? CreatedAt { get; set; }
}
