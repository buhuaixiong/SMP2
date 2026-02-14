namespace SupplierSystem.Domain.Entities;

public sealed class SupplierDraft
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string DraftData { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
    public string UpdatedAt { get; set; } = null!;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
