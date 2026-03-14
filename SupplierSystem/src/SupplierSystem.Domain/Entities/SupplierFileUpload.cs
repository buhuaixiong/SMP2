namespace SupplierSystem.Domain.Entities;

public sealed class SupplierFileUpload
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public int FileId { get; set; }
    public string? FileName { get; set; }
    public string? FileDescription { get; set; }
    public string? Status { get; set; }
    public string? CurrentStep { get; set; }
    public string? SubmittedBy { get; set; }
    public string? SubmittedAt { get; set; }
    public string? RiskLevel { get; set; }
    public string? ValidFrom { get; set; }
    public string? ValidTo { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
