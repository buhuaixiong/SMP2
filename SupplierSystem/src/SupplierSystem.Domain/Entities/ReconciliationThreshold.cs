namespace SupplierSystem.Domain.Entities;

public sealed class ReconciliationThreshold
{
    public int Id { get; set; }
    public string ThresholdName { get; set; } = null!;
    public decimal? AcceptableVariancePercentage { get; set; }
    public decimal? WarningVariancePercentage { get; set; }
    public bool? AutoMatchEnabled { get; set; }
    public string? Description { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
