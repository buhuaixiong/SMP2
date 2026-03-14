using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class QuoteEntityConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes", table => table.HasTrigger("trg_dbo_quotes_audit_update"));
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.BidRoundId).HasColumnName("bid_round_id");
        builder.Property(entity => entity.SupplierId).HasColumnName("supplier_id");
        builder.Property(entity => entity.UnitPrice).HasColumnName("unit_price");
        builder.Property(entity => entity.TotalAmount).HasColumnName("total_amount");
        builder.Property(entity => entity.Currency).HasColumnName("currency");
        builder.Property(entity => entity.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(entity => entity.PaymentTerms).HasColumnName("payment_terms");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(entity => entity.WithdrawalReason).HasColumnName("withdrawal_reason");
        builder.Property(entity => entity.WithdrawnAt).HasColumnName("withdrawn_at");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entity => entity.Brand).HasColumnName("brand");
        builder.Property(entity => entity.TaxStatus).HasColumnName("tax_status");
        builder.Property(entity => entity.Parameters).HasColumnName("parameters");
        builder.Property(entity => entity.OptionalConfig).HasColumnName("optional_config");
        builder.Property(entity => entity.Version).HasColumnName("version");
        builder.Property(entity => entity.IsLatest).HasColumnName("is_latest");
        builder.Property(entity => entity.ModifiedCount).HasColumnName("modified_count");
        builder.Property(entity => entity.IpAddress).HasColumnName("ip_address");
        builder.Property(entity => entity.CanModifyUntil).HasColumnName("can_modify_until");
        builder.Property(entity => entity.DeliveryTerms).HasColumnName("delivery_terms");
        builder.Property(entity => entity.ShippingLocation).HasColumnName("shipping_location");
        builder.Property(entity => entity.ShippingCountry).HasColumnName("shipping_country");
        builder.Property(entity => entity.TotalStandardCostLocal).HasColumnName("total_standard_cost_local");
        builder.Property(entity => entity.TotalStandardCostUsd).HasColumnName("total_standard_cost_usd");
        builder.Property(entity => entity.TotalTariffAmountLocal).HasColumnName("total_tariff_amount_local");
        builder.Property(entity => entity.TotalTariffAmountUsd).HasColumnName("total_tariff_amount_usd");
        builder.Property(entity => entity.HasSpecialTariff).HasColumnName("has_special_tariff");
    }
}
