namespace SupplierSystem.Domain.Entities;

public sealed class SupplierUpgradeDocument
{
    public int Id { get; set; }
    public int ApplicationId { get; set; }
    public string RequirementCode { get; set; } = null!;
    public string RequirementName { get; set; } = null!;
    public int FileId { get; set; }
    public string? UploadedAt { get; set; }
    public string? UploadedBy { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}
