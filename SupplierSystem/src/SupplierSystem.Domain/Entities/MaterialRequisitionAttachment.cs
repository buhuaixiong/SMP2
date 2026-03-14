namespace SupplierSystem.Domain.Entities;

public sealed class MaterialRequisitionAttachment
{
    public int Id { get; set; }
    public int RequisitionId { get; set; }
    public string OriginalName { get; set; } = null!;
    public string StoredName { get; set; } = null!;
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
}
