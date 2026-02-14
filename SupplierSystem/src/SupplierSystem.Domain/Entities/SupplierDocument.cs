namespace SupplierSystem.Domain.Entities;

public sealed class SupplierDocument
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? DocType { get; set; }
    public string? StoredName { get; set; }
    public string? OriginalName { get; set; }
    public string? UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public string? ValidFrom { get; set; }
    public string? ExpiresAt { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public long? FileSize { get; set; }
    public string? Category { get; set; }
    public bool IsRequired { get; set; }
}
