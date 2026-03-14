using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqAttachmentEntityConfiguration : IEntityTypeConfiguration<RfqAttachment>
{
    public void Configure(EntityTypeBuilder<RfqAttachment> builder)
    {
        builder.ToTable("rfq_attachments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.LineItemId).HasColumnName("line_item_id");
        builder.Property(entity => entity.FileName).HasColumnName("file_name");
        builder.Property(entity => entity.FilePath).HasColumnName("file_path");
        builder.Property(entity => entity.FileSize).HasColumnName("file_size");
        builder.Property(entity => entity.FileType).HasColumnName("file_type");
        builder.Property(entity => entity.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(entity => entity.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(entity => entity.Description).HasColumnName("description");
    }
}
