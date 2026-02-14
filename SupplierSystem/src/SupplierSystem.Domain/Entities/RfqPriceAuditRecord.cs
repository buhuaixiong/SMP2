namespace SupplierSystem.Domain.Entities;

public sealed class RfqPriceAuditRecord
{
    public long Id { get; set; }
    public long RfqId { get; set; }
    public string? RfqTitle { get; set; }
    public string? RfqCreatedAt { get; set; }
    public long? RfqLineItemId { get; set; }
    public int? LineNumber { get; set; }
    public decimal? Quantity { get; set; }
    public long? QuoteId { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierIp { get; set; }
    public decimal? QuotedUnitPrice { get; set; }
    public decimal? QuotedTotalPrice { get; set; }
    public string? QuoteCurrency { get; set; }
    public string? QuoteSubmittedAt { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalDecision { get; set; }
    public string? ApprovalDecidedAt { get; set; }
    public long? SelectedQuoteId { get; set; }
    public long? SelectedSupplierId { get; set; }
    public string? SelectedSupplierName { get; set; }
    public decimal? SelectedUnitPrice { get; set; }
    public string? SelectedCurrency { get; set; }
    public string? PrFilledBy { get; set; }
    public string? PrFilledAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
