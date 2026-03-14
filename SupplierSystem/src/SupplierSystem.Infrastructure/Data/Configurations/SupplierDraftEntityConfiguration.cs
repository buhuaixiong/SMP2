using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierDraftEntityConfiguration : IEntityTypeConfiguration<SupplierDraft>
{
    public void Configure(EntityTypeBuilder<SupplierDraft> builder)
    {
        builder.ToTable("supplier_drafts");
        builder.HasKey(d => d.Id);
    }
}
