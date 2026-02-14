namespace SupplierSystem.Domain.Entities;

public sealed class Quote
{
    public long Id { get; set; }
    public long RfqId { get; set; }
    public int SupplierId { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Currency { get; set; }
    public string? DeliveryDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
    public string? SubmittedAt { get; set; }
    public string? WithdrawalReason { get; set; }
    public string? WithdrawnAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Brand { get; set; }
    public string? TaxStatus { get; set; }
    public string? Parameters { get; set; }
    public string? OptionalConfig { get; set; }
    public int? Version { get; set; }
    public bool IsLatest { get; set; }
    public int? ModifiedCount { get; set; }
    public string? IpAddress { get; set; }
    public string? CanModifyUntil { get; set; }
    public string? DeliveryTerms { get; set; }
    public string? ShippingLocation { get; set; }
    public string? ShippingCountry { get; set; }
    public decimal? TotalStandardCostLocal { get; set; }
    public decimal? TotalStandardCostUsd { get; set; }
    public decimal? TotalTariffAmountLocal { get; set; }
    public decimal? TotalTariffAmountUsd { get; set; }
    public bool HasSpecialTariff { get; set; }
}
