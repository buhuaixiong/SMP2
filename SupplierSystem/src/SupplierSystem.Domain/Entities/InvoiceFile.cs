namespace SupplierSystem.Domain.Entities;

public sealed class InvoiceFile
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int? ReconciliationId { get; set; }
    public int? SupplierId { get; set; }
    public string OriginalName { get; set; } = null!;
    public string StoredName { get; set; } = null!;
    public string StoragePath { get; set; } = null!;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? UploadedBy { get; set; }
    public string? UploadedByName { get; set; }
    public string? UploadedAt { get; set; }
    public string? Checksum { get; set; }
    public string? DeletedAt { get; set; }
}
