using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class QuoteAttachmentEntityConfiguration : IEntityTypeConfiguration<QuoteAttachment>
{
    public void Configure(EntityTypeBuilder<QuoteAttachment> builder)
    {
        builder.ToTable("quote_attachments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id)
            .HasColumnName("id")
            .HasColumnType("bigint");
        builder.Property(entity => entity.QuoteId)
            .HasColumnName("quote_id")
            .HasColumnType("bigint");
        builder.Property(entity => entity.OriginalName).HasColumnName("original_name");
        builder.Property(entity => entity.StoredName).HasColumnName("stored_name");
        builder.Property(entity => entity.FileType).HasColumnName("file_type");
        builder.Property(entity => entity.FileSize)
            .HasColumnName("file_size")
            .HasColumnType("int");
        builder.Property(entity => entity.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(entity => entity.UploadedBy)
            .HasColumnName("uploaded_by")
            .HasColumnType("bigint");
    }
}
