using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class QuoteVersionEntityConfiguration : IEntityTypeConfiguration<QuoteVersion>
{
    public void Configure(EntityTypeBuilder<QuoteVersion> builder)
    {
        builder.ToTable("quote_versions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.QuoteId).HasColumnName("quote_id");
        builder.Property(entity => entity.Version).HasColumnName("version");
        builder.Property(entity => entity.UnitPrice).HasColumnName("unit_price");
        builder.Property(entity => entity.TotalAmount).HasColumnName("total_amount");
        builder.Property(entity => entity.Brand).HasColumnName("brand");
        builder.Property(entity => entity.TaxStatus).HasColumnName("tax_status");
        builder.Property(entity => entity.Parameters).HasColumnName("parameters");
        builder.Property(entity => entity.OptionalConfig).HasColumnName("optional_config");
        builder.Property(entity => entity.DeliveryDate).HasColumnName("delivery_date");
        builder.Property(entity => entity.PaymentTerms).HasColumnName("payment_terms");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.ModifiedAt).HasColumnName("modified_at");
        builder.Property(entity => entity.IpAddress).HasColumnName("ip_address");
        builder.Property(entity => entity.ChangeSummary).HasColumnName("change_summary");
    }
}
