namespace SupplierSystem.Domain.Entities;

public sealed class TemplateDocument
{
    public int Id { get; set; }
    public string TemplateCode { get; set; } = null!;
    public string TemplateName { get; set; } = null!;
    public string? Description { get; set; }
    public string StoredName { get; set; } = null!;
    public string OriginalName { get; set; } = null!;
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? UploadedBy { get; set; }
    public string? UploadedAt { get; set; }
    public bool IsActive { get; set; }
}
