using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class PurchaseOrderEntityConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.PoNumber).HasColumnName("po_number");
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.SupplierId).HasColumnName("supplier_id");
        builder.Property(entity => entity.TotalAmount).HasColumnName("total_amount");
        builder.Property(entity => entity.Currency).HasColumnName("currency");
        builder.Property(entity => entity.ItemCount).HasColumnName("item_count");
        builder.Property(entity => entity.PoFilePath).HasColumnName("po_file_path");
        builder.Property(entity => entity.PoFileName).HasColumnName("po_file_name");
        builder.Property(entity => entity.PoFileSize).HasColumnName("po_file_size");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.Description).HasColumnName("description");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
    }
}
