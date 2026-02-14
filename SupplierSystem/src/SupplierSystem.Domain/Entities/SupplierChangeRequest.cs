namespace SupplierSystem.Domain.Entities;

public sealed class SupplierChangeRequest
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? ChangeType { get; set; }
    public string? RiskLevel { get; set; }
    public string? Status { get; set; }
    public string? CurrentStep { get; set; }
    public string? Payload { get; set; }
    public string? SubmittedBy { get; set; }
    public string? SubmittedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public bool? RequiresQuality { get; set; }
}
