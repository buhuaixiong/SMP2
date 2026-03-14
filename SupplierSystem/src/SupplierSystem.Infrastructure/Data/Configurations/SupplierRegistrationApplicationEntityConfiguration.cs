using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierRegistrationApplicationEntityConfiguration : IEntityTypeConfiguration<SupplierRegistrationApplication>
{
    public void Configure(EntityTypeBuilder<SupplierRegistrationApplication> builder)
    {
        builder.ToTable("supplier_registration_applications_v", table => table.HasTrigger("trg_dbo_supplier_registration_applications_audit_update"));
        builder.HasKey(a => a.Id);
    }
}
