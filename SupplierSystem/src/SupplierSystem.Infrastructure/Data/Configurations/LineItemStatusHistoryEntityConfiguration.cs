using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class LineItemStatusHistoryEntityConfiguration : IEntityTypeConfiguration<LineItemStatusHistory>
{
    public void Configure(EntityTypeBuilder<LineItemStatusHistory> builder)
    {
        builder.ToTable("line_item_status_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.LineItemId).HasColumnName("line_item_id");
        builder.Property(entity => entity.FromStatus).HasColumnName("from_status");
        builder.Property(entity => entity.ToStatus).HasColumnName("to_status");
        builder.Property(entity => entity.ChangedBy).HasColumnName("changed_by");
        builder.Property(entity => entity.ChangedAt).HasColumnName("changed_at");
        builder.Property(entity => entity.Reason).HasColumnName("reason");
    }
}
