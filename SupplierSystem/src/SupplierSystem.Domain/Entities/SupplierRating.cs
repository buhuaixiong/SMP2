namespace SupplierSystem.Domain.Entities;

public sealed class SupplierRating
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public decimal? OnTimeDelivery { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? ServiceScore { get; set; }
    public decimal? CostScore { get; set; }
    public decimal? OverallScore { get; set; }
    public string? Notes { get; set; }
    public string? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
