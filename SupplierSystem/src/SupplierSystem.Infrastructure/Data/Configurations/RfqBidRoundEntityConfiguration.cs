using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqBidRoundEntityConfiguration : IEntityTypeConfiguration<RfqBidRound>
{
    public void Configure(EntityTypeBuilder<RfqBidRound> builder)
    {
        builder.ToTable("rfq_bid_rounds");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.RoundNumber).HasColumnName("round_number");
        builder.Property(entity => entity.BidDeadline).HasColumnName("bid_deadline");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.OpenedAt).HasColumnName("opened_at");
        builder.Property(entity => entity.ClosedAt).HasColumnName("closed_at");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entity => entity.ExtensionReason).HasColumnName("extension_reason");
        builder.Property(entity => entity.StartedFromRoundId).HasColumnName("started_from_round_id");

        builder.HasIndex(entity => new { entity.RfqId, entity.RoundNumber }).IsUnique();
        builder.HasIndex(entity => new { entity.RfqId, entity.Status });
    }
}
