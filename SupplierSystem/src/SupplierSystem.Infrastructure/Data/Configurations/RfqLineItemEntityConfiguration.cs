using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqLineItemEntityConfiguration : IEntityTypeConfiguration<RfqLineItem>
{
    public void Configure(EntityTypeBuilder<RfqLineItem> builder)
    {
        builder.ToTable("rfq_line_items", table => table.HasTrigger("trg_dbo_rfq_line_items_audit_update"));
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.LineNumber).HasColumnName("line_number");
        builder.Property(entity => entity.MaterialCategory).HasColumnName("material_category");
        builder.Property(entity => entity.Brand).HasColumnName("brand");
        builder.Property(entity => entity.ItemName).HasColumnName("item_name");
        builder.Property(entity => entity.Specifications).HasColumnName("specifications");
        builder.Property(entity => entity.Quantity).HasColumnName("quantity");
        builder.Property(entity => entity.Unit).HasColumnName("unit");
        builder.Property(entity => entity.EstimatedUnitPrice).HasColumnName("estimated_unit_price");
        builder.Property(entity => entity.Currency).HasColumnName("currency");
        builder.Property(entity => entity.Parameters).HasColumnName("parameters");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.CurrentApproverRole).HasColumnName("current_approver_role");
        builder.Property(entity => entity.SelectedQuoteId).HasColumnName("selected_quote_id");
        builder.Property(entity => entity.PoId).HasColumnName("po_id");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
    }
}
