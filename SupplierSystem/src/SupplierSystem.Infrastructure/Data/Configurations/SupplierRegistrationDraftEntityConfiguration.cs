using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierRegistrationDraftEntityConfiguration : IEntityTypeConfiguration<SupplierRegistrationDraft>
{
    public void Configure(EntityTypeBuilder<SupplierRegistrationDraft> builder)
    {
        builder.ToTable("supplier_registration_drafts");
        builder.HasKey(d => d.Id);
    }
}
