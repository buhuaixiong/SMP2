namespace SupplierSystem.Domain.Entities;

public sealed class BuyerSupplierAssignment
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = null!;
    public int SupplierId { get; set; }
    public string? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
