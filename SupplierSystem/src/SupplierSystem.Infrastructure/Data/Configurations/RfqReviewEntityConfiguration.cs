using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqReviewEntityConfiguration : IEntityTypeConfiguration<RfqReview>
{
    public void Configure(EntityTypeBuilder<RfqReview> builder)
    {
        builder.ToTable("rfq_reviews");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.SelectedQuoteId).HasColumnName("selected_quote_id");
        builder.Property(entity => entity.ReviewScores).HasColumnName("review_scores");
        builder.Property(entity => entity.Comments).HasColumnName("comments");
        builder.Property(entity => entity.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(entity => entity.ReviewedAt).HasColumnName("reviewed_at");
    }
}
