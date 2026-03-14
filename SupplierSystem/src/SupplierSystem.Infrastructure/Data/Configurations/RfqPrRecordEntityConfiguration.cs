using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqPrRecordEntityConfiguration : IEntityTypeConfiguration<RfqPrRecord>
{
    public void Configure(EntityTypeBuilder<RfqPrRecord> builder)
    {
        builder.ToTable("rfq_pr_records");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.PrNumber).HasColumnName("pr_number");
        builder.Property(entity => entity.PrDate).HasColumnName("pr_date");
        builder.Property(entity => entity.FilledBy).HasColumnName("filled_by");
        builder.Property(entity => entity.FilledAt).HasColumnName("filled_at");
        builder.Property(entity => entity.DepartmentConfirmerId).HasColumnName("department_confirmer_id");
        builder.Property(entity => entity.DepartmentConfirmerName).HasColumnName("department_confirmer_name");
        builder.Property(entity => entity.ConfirmationStatus).HasColumnName("confirmation_status");
        builder.Property(entity => entity.ConfirmationNotes).HasColumnName("confirmation_notes");
        builder.Property(entity => entity.ConfirmedAt).HasColumnName("confirmed_at");
    }
}
