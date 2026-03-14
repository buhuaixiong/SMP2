namespace SupplierSystem.Domain.Entities;

public sealed class QuoteVersion
{
    public long Id { get; set; }
    public long QuoteId { get; set; }
    public int Version { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Brand { get; set; }
    public string? TaxStatus { get; set; }
    public string? Parameters { get; set; }
    public string? OptionalConfig { get; set; }
    public string? DeliveryDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public string? ModifiedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? ChangeSummary { get; set; }
}
