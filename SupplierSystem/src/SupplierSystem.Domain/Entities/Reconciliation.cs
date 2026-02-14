namespace SupplierSystem.Domain.Entities;

public sealed class Reconciliation
{
    public int Id { get; set; }
    public string ReconciliationNumber { get; set; } = null!;
    public int SupplierId { get; set; }
    public int? WarehouseReceiptId { get; set; }
    public string? PeriodStart { get; set; }
    public string? PeriodEnd { get; set; }
    public decimal? TotalInvoiceAmount { get; set; }
    public decimal? TotalReceiptAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public decimal? VariancePercentage { get; set; }
    public string? Status { get; set; }
    public string? MatchType { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAt { get; set; }
    public int? ConfirmedBy { get; set; }
    public string? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public string? UpdatedAt { get; set; }
}
