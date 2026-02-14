namespace SupplierSystem.Domain.Entities;

public sealed class PriceComparisonAttachment
{
    public int Id { get; set; }
    public int RfqId { get; set; }
    public int? LineItemId { get; set; }
    public string Platform { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string? ProductUrl { get; set; }
    public decimal? PlatformPrice { get; set; }
    public string UploadedBy { get; set; } = null!;
    public string? UploadedAt { get; set; }
    public string? Notes { get; set; }
    public string? StoredFileName { get; set; }
    public string? OriginalFileName { get; set; }
}
