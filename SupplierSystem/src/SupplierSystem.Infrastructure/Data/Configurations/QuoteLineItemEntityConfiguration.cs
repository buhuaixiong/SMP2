using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class QuoteLineItemEntityConfiguration : IEntityTypeConfiguration<QuoteLineItem>
{
    public void Configure(EntityTypeBuilder<QuoteLineItem> builder)
    {
        builder.ToTable("quote_line_items", table => table.HasTrigger("trg_dbo_quote_line_items_audit_update"));
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.QuoteId).HasColumnName("quote_id");
        builder.Property(entity => entity.RfqLineItemId).HasColumnName("rfq_line_item_id");
        builder.Property(entity => entity.UnitPrice).HasColumnName("unit_price");
        builder.Property(entity => entity.MinimumOrderQuantity).HasColumnName("minimum_order_quantity");
        builder.Property(entity => entity.StandardPackageQuantity).HasColumnName("standard_package_quantity");
        builder.Property(entity => entity.TotalPrice).HasColumnName("total_price");
        builder.Property(entity => entity.Brand).HasColumnName("brand");
        builder.Property(entity => entity.TaxStatus).HasColumnName("tax_status");
        builder.Property(entity => entity.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(entity => entity.DeliveryPeriod).HasColumnName("delivery_period");
        builder.Property(entity => entity.Parameters).HasColumnName("parameters");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.ProductOrigin).HasColumnName("product_origin");
        builder.Property(entity => entity.ProductGroup).HasColumnName("product_group");
        builder.Property(entity => entity.OriginalPriceUsd).HasColumnName("original_price_usd");
        builder.Property(entity => entity.ExchangeRate).HasColumnName("exchange_rate");
        builder.Property(entity => entity.ExchangeRateDate).HasColumnName("exchange_rate_date");
        builder.Property(entity => entity.TariffRate).HasColumnName("tariff_rate");
        builder.Property(entity => entity.TariffRatePercent).HasColumnName("tariff_rate_percent");
        builder.Property(entity => entity.TariffAmountLocal).HasColumnName("tariff_amount_local");
        builder.Property(entity => entity.TariffAmountUsd).HasColumnName("tariff_amount_usd");
        builder.Property(entity => entity.SpecialTariffRate).HasColumnName("special_tariff_rate");
        builder.Property(entity => entity.SpecialTariffRatePercent).HasColumnName("special_tariff_rate_percent");
        builder.Property(entity => entity.SpecialTariffAmountLocal).HasColumnName("special_tariff_amount_local");
        builder.Property(entity => entity.SpecialTariffAmountUsd).HasColumnName("special_tariff_amount_usd");
        builder.Property(entity => entity.HasSpecialTariff).HasColumnName("has_special_tariff");
        builder.Property(entity => entity.StandardCostLocal).HasColumnName("standard_cost_local");
        builder.Property(entity => entity.StandardCostUsd).HasColumnName("standard_cost_usd");
        builder.Property(entity => entity.StandardCostCurrency).HasColumnName("standard_cost_currency");
        builder.Property(entity => entity.CalculatedAt).HasColumnName("calculated_at");
    }
}
