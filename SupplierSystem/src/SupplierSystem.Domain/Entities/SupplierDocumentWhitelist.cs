namespace SupplierSystem.Domain.Entities;

public sealed class SupplierDocumentWhitelist
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string DocumentType { get; set; } = null!;
    public string ExemptedBy { get; set; } = null!;
    public string? ExemptedByName { get; set; }
    public string? ExemptedAt { get; set; }
    public string? Reason { get; set; }
    public string? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
