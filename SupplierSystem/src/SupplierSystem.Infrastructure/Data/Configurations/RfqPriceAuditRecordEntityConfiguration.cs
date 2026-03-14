using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqPriceAuditRecordEntityConfiguration : IEntityTypeConfiguration<RfqPriceAuditRecord>
{
    public void Configure(EntityTypeBuilder<RfqPriceAuditRecord> builder)
    {
        var stringToDateTimeConverter = new ValueConverter<string?, DateTime?>(
            value => string.IsNullOrWhiteSpace(value)
                ? null
                : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            value => value.HasValue
                ? value.Value.ToString("o", CultureInfo.InvariantCulture)
                : null);

        builder.ToTable("rfq_price_audit");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.BidRoundId).HasColumnName("bid_round_id");
        builder.Property(entity => entity.RoundNumber).HasColumnName("round_number");
        builder.Property(entity => entity.RfqTitle).HasColumnName("rfq_title");
        builder.Property(entity => entity.RfqCreatedAt)
            .HasColumnName("rfq_created_at")
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.RfqLineItemId).HasColumnName("rfq_line_item_id");
        builder.Property(entity => entity.LineNumber).HasColumnName("line_number");
        builder.Property(entity => entity.Quantity)
            .HasColumnName("quantity")
            .HasColumnType("decimal(18,6)");
        builder.Property(entity => entity.QuoteId).HasColumnName("quote_id");
        builder.Property(entity => entity.SupplierId).HasColumnName("supplier_id");
        builder.Property(entity => entity.SupplierName).HasColumnName("supplier_name");
        builder.Property(entity => entity.SupplierIp).HasColumnName("supplier_ip");
        builder.Property(entity => entity.QuotedUnitPrice)
            .HasColumnName("quoted_unit_price")
            .HasColumnType("decimal(18,6)");
        builder.Property(entity => entity.QuotedTotalPrice)
            .HasColumnName("quoted_total_price")
            .HasColumnType("decimal(18,6)");
        builder.Property(entity => entity.QuoteCurrency).HasColumnName("quote_currency");
        builder.Property(entity => entity.QuoteSubmittedAt)
            .HasColumnName("quote_submitted_at")
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.ApprovalStatus).HasColumnName("approval_status");
        builder.Property(entity => entity.ApprovalDecision).HasColumnName("approval_decision");
        builder.Property(entity => entity.ApprovalDecidedAt)
            .HasColumnName("approval_decided_at")
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.SelectedQuoteId).HasColumnName("selected_quote_id");
        builder.Property(entity => entity.SelectedSupplierId).HasColumnName("selected_supplier_id");
        builder.Property(entity => entity.SelectedSupplierName).HasColumnName("selected_supplier_name");
        builder.Property(entity => entity.SelectedUnitPrice)
            .HasColumnName("selected_unit_price")
            .HasColumnType("decimal(18,6)");
        builder.Property(entity => entity.SelectedCurrency).HasColumnName("selected_currency");
        builder.Property(entity => entity.PrFilledBy).HasColumnName("pr_filled_by");
        builder.Property(entity => entity.PrFilledAt)
            .HasColumnName("pr_filled_at")
            .HasConversion(stringToDateTimeConverter)
            .HasColumnType("datetime2(3)");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
    }
}
