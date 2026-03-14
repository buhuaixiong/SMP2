namespace SupplierSystem.Domain.Entities;

public sealed class SupplierRiskAssessment
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public string? RiskLevel { get; set; }
    public decimal? RiskScore { get; set; }
    public string? RiskType { get; set; }
    public string? AssessmentDate { get; set; }
    public int? AssessedBy { get; set; }
    public string? RiskFactors { get; set; }
    public string? MitigationRecommendations { get; set; }
}
