using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierTagEntityConfiguration : IEntityTypeConfiguration<SupplierTag>
{
    public void Configure(EntityTypeBuilder<SupplierTag> builder)
    {
        builder.ToTable("supplier_tags");
        builder.HasKey(st => new { st.SupplierId, st.TagId });
        builder.Property(st => st.SupplierId).HasColumnName("supplierId");
        builder.Property(st => st.TagId).HasColumnName("tagId");
    }
}
