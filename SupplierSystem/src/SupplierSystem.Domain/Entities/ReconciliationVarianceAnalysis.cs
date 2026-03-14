namespace SupplierSystem.Domain.Entities;

public sealed class ReconciliationVarianceAnalysis
{
    public int Id { get; set; }
    public int ReconciliationId { get; set; }
    public string VarianceType { get; set; } = null!;
    public decimal VarianceAmount { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? VariancePercentage { get; set; }
    public string? Severity { get; set; }
    public string? RootCause { get; set; }
    public string? ResolutionAction { get; set; }
    public string? Status { get; set; }
    public int? AssignedTo { get; set; }
    public string? ResolvedAt { get; set; }
    public int? ResolvedBy { get; set; }
    public string? Notes { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
