using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqLineItemApprovalHistoryEntityConfiguration : IEntityTypeConfiguration<RfqLineItemApprovalHistory>
{
    public void Configure(EntityTypeBuilder<RfqLineItemApprovalHistory> builder)
    {
        builder.ToTable("rfq_line_item_approval_history");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqLineItemId).HasColumnName("rfq_line_item_id");
        builder.Property(entity => entity.Step).HasColumnName("step");
        builder.Property(entity => entity.ApproverId).HasColumnName("approver_id");
        builder.Property(entity => entity.ApproverName).HasColumnName("approver_name");
        builder.Property(entity => entity.ApproverRole).HasColumnName("approver_role");
        builder.Property(entity => entity.Decision).HasColumnName("decision");
        builder.Property(entity => entity.Comments).HasColumnName("comments");
        builder.Property(entity => entity.PreviousQuoteId).HasColumnName("previous_quote_id");
        builder.Property(entity => entity.NewQuoteId).HasColumnName("new_quote_id");
        builder.Property(entity => entity.ChangeReason).HasColumnName("change_reason");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
    }
}
