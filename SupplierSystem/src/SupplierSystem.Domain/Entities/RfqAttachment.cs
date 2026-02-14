namespace SupplierSystem.Domain.Entities;

public sealed class RfqAttachment
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public int? LineItemId { get; set; }
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public long? FileSize { get; set; }
    public string? FileType { get; set; }
    public string UploadedBy { get; set; } = null!;
    public string? UploadedAt { get; set; }
    public string? Description { get; set; }
}
