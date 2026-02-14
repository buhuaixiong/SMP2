namespace SupplierSystem.Domain.Entities;

public sealed class PurchasingGroupSupplier
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SupplierId { get; set; }
    public string? AssignedAt { get; set; }
    public string? AssignedBy { get; set; }
    public bool? IsPrimary { get; set; }
    public string? Notes { get; set; }
}
