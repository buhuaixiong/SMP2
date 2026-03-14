namespace SupplierSystem.Domain.Entities;

public sealed class BuyerSupplierAccessCache
{
    public string BuyerId { get; set; } = null!;
    public int SupplierId { get; set; }
    public string AccessType { get; set; } = null!;
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public string? LastUpdated { get; set; }
}
