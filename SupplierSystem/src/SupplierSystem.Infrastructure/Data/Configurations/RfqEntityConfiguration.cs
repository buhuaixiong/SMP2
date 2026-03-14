using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqEntityConfiguration : IEntityTypeConfiguration<Rfq>
{
    public void Configure(EntityTypeBuilder<Rfq> builder)
    {
        builder.ToTable("rfqs", table => table.HasTrigger("trg_dbo_rfqs_audit_update"));
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Title).HasColumnName("title");
        builder.Property(entity => entity.Description).HasColumnName("description");
        builder.Property(entity => entity.Amount).HasColumnName("amount");
        builder.Property(entity => entity.Currency).HasColumnName("currency");
        builder.Property(entity => entity.DeliveryPeriod).HasColumnName("delivery_period");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entity => entity.MaterialType).HasColumnName("material_type");
        builder.Property(entity => entity.MaterialCategoryType).HasColumnName("material_category_type");
        builder.Property(entity => entity.IsLineItemMode).HasColumnName("is_line_item_mode");
        builder.Property(entity => entity.DistributionCategory).HasColumnName("distribution_category");
        builder.Property(entity => entity.DistributionSubcategory).HasColumnName("distribution_subcategory");
        builder.Property(entity => entity.RfqType).HasColumnName("rfq_type");
        builder.Property(entity => entity.BudgetAmount).HasColumnName("budget_amount");
        builder.Property(entity => entity.RequiredDocuments).HasColumnName("required_documents");
        builder.Property(entity => entity.EvaluationCriteria).HasColumnName("evaluation_criteria");
        builder.Property(entity => entity.ValidUntil).HasColumnName("valid_until");
        builder.Property(entity => entity.RequestingParty).HasColumnName("requesting_party");
        builder.Property(entity => entity.RequestingDepartment).HasColumnName("requesting_department");
        builder.Property(entity => entity.RequirementDate).HasColumnName("requirement_date");
        builder.Property(entity => entity.DetailedParameters).HasColumnName("detailed_parameters");
        builder.Property(entity => entity.MinSupplierCount).HasColumnName("min_supplier_count");
        builder.Property(entity => entity.SupplierExceptionNote).HasColumnName("supplier_exception_note");
        builder.Property(entity => entity.SelectedQuoteId).HasColumnName("selected_quote_id");
        builder.Property(entity => entity.ReviewCompletedAt).HasColumnName("review_completed_at");
        builder.Property(entity => entity.ApprovalStatus).HasColumnName("approval_status");
        builder.Property(entity => entity.PrStatus).HasColumnName("pr_status");
        builder.Property(entity => entity.RequisitionId).HasColumnName("requisition_id");
        builder.Property(entity => entity.ViewLink).HasColumnName("view_link");
    }
}
