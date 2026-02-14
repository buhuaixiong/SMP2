namespace SupplierSystem.Domain.Entities;

public sealed class QuoteAttachment
{
    public long Id { get; set; }
    public long QuoteId { get; set; }
    public string OriginalName { get; set; } = null!;
    public string StoredName { get; set; } = null!;
    public string? FileType { get; set; }
    public int? FileSize { get; set; }
    public string? UploadedAt { get; set; }
    public long? UploadedBy { get; set; }
}
