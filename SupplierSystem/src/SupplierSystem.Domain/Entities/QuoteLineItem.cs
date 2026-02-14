namespace SupplierSystem.Domain.Entities;

public sealed class QuoteLineItem
{
    public long Id { get; set; }
    public long QuoteId { get; set; }
    public long RfqLineItemId { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? MinimumOrderQuantity { get; set; }
    public decimal? StandardPackageQuantity { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Brand { get; set; }
    public string? TaxStatus { get; set; }
    public string? DeliveryDate { get; set; }
    public int? DeliveryPeriod { get; set; }
    public string? Parameters { get; set; }
    public string? Notes { get; set; }
    public string? ProductOrigin { get; set; }
    public string? ProductGroup { get; set; }
    public decimal? OriginalPriceUsd { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? ExchangeRateDate { get; set; }
    public decimal? TariffRate { get; set; }
    public decimal? TariffRatePercent { get; set; }
    public decimal? TariffAmountLocal { get; set; }
    public decimal? TariffAmountUsd { get; set; }
    public decimal? SpecialTariffRate { get; set; }
    public decimal? SpecialTariffRatePercent { get; set; }
    public decimal? SpecialTariffAmountLocal { get; set; }
    public decimal? SpecialTariffAmountUsd { get; set; }
    public bool HasSpecialTariff { get; set; }
    public decimal? StandardCostLocal { get; set; }
    public decimal? StandardCostUsd { get; set; }
    public string? StandardCostCurrency { get; set; }
    public string? CalculatedAt { get; set; }
}
