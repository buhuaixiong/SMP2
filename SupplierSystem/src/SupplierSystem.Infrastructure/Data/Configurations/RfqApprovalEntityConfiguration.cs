using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqApprovalEntityConfiguration : IEntityTypeConfiguration<RfqApproval>
{
    public void Configure(EntityTypeBuilder<RfqApproval> builder)
    {
        builder.ToTable("rfq_approvals");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.StepOrder).HasColumnName("step_order");
        builder.Property(entity => entity.StepName).HasColumnName("step_name");
        builder.Property(entity => entity.ApproverRole).HasColumnName("approver_role");
        builder.Property(entity => entity.ApproverId).HasColumnName("approver_id");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.Decision).HasColumnName("decision");
        builder.Property(entity => entity.DecidedAt).HasColumnName("decided_at");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
    }
}
