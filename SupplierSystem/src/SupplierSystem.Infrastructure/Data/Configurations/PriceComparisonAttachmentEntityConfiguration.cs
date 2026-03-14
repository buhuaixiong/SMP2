using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class PriceComparisonAttachmentEntityConfiguration : IEntityTypeConfiguration<PriceComparisonAttachment>
{
    public void Configure(EntityTypeBuilder<PriceComparisonAttachment> builder)
    {
        builder.ToTable("price_comparison_attachments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.BidRoundId).HasColumnName("bid_round_id");
        builder.Property(entity => entity.LineItemId).HasColumnName("line_item_id");
        builder.Property(entity => entity.Platform).HasColumnName("platform");
        builder.Property(entity => entity.FileName).HasColumnName("file_name");
        builder.Property(entity => entity.FilePath).HasColumnName("file_path");
        builder.Property(entity => entity.ProductUrl).HasColumnName("product_url");
        builder.Property(entity => entity.PlatformPrice).HasColumnName("platform_price");
        builder.Property(entity => entity.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(entity => entity.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.StoredFileName).HasColumnName("stored_file_name");
        builder.Property(entity => entity.OriginalFileName).HasColumnName("original_file_name");
    }
}
