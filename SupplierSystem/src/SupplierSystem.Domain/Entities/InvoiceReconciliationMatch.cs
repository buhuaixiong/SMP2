namespace SupplierSystem.Domain.Entities;

public sealed class InvoiceReconciliationMatch
{
    public int Id { get; set; }
    public int ReconciliationId { get; set; }
    public int InvoiceId { get; set; }
    public int? WarehouseReceiptId { get; set; }
    public string? MatchType { get; set; }
    public decimal? MatchConfidence { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal? ReceiptAmount { get; set; }
    public decimal? VarianceAmount { get; set; }
    public decimal? VariancePercentage { get; set; }
    public string? MatchedAt { get; set; }
    public int? MatchedBy { get; set; }
    public string? Notes { get; set; }
}
