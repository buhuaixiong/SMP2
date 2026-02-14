namespace SupplierSystem.Domain.Entities;

public sealed class BuyerSupplierPermission
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = null!;
    public int SupplierId { get; set; }
    public bool CanViewProfile { get; set; }
    public bool ReceiveContractAlerts { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
