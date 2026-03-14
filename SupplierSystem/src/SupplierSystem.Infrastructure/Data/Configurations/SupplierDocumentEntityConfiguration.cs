using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierDocumentEntityConfiguration : IEntityTypeConfiguration<SupplierDocument>
{
    public void Configure(EntityTypeBuilder<SupplierDocument> builder)
    {
        builder.ToTable("supplier_documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.SupplierId).HasColumnName("supplierId");
        builder.Property(d => d.DocType).HasColumnName("docType");
        builder.Property(d => d.StoredName).HasColumnName("storedName");
        builder.Property(d => d.OriginalName).HasColumnName("originalName");
        builder.Property(d => d.UploadedAt).HasColumnName("uploadedAt");
        builder.Property(d => d.UploadedBy).HasColumnName("uploadedBy");
        builder.Property(d => d.ValidFrom).HasColumnName("validFrom");
        builder.Property(d => d.ExpiresAt).HasColumnName("expiresAt");
        builder.Property(d => d.Status).HasColumnName("status");
        builder.Property(d => d.Notes).HasColumnName("notes");
        builder.Property(d => d.FileSize).HasColumnName("fileSize");
        builder.Property(d => d.Category).HasColumnName("category");
        builder.Property(d => d.IsRequired).HasColumnName("isRequired");
    }
}
