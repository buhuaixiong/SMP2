using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierDocumentWhitelistEntityConfiguration : IEntityTypeConfiguration<SupplierDocumentWhitelist>
{
    public void Configure(EntityTypeBuilder<SupplierDocumentWhitelist> builder)
    {
        builder.ToTable("supplier_document_whitelist");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.SupplierId).HasColumnName("supplier_id");
        builder.Property(entity => entity.DocumentType).HasColumnName("document_type");
        builder.Property(entity => entity.ExemptedBy).HasColumnName("exempted_by");
        builder.Property(entity => entity.ExemptedByName).HasColumnName("exempted_by_name");
        builder.Property(entity => entity.ExemptedAt).HasColumnName("exempted_at");
        builder.Property(entity => entity.Reason).HasColumnName("reason");
        builder.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        builder.Property(entity => entity.IsActive).HasColumnName("is_active");
    }
}
