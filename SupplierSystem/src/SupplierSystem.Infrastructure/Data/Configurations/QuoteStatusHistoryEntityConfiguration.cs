using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class QuoteStatusHistoryEntityConfiguration : IEntityTypeConfiguration<QuoteStatusHistory>
{
    public void Configure(EntityTypeBuilder<QuoteStatusHistory> builder)
    {
        builder.ToTable("quote_status_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.QuoteId).HasColumnName("quote_id");
        builder.Property(entity => entity.FromStatus).HasColumnName("from_status");
        builder.Property(entity => entity.ToStatus).HasColumnName("to_status");
        builder.Property(entity => entity.ChangedBy).HasColumnName("changed_by");
        builder.Property(entity => entity.ChangedAt).HasColumnName("changed_at");
        builder.Property(entity => entity.Reason).HasColumnName("reason");
    }
}
