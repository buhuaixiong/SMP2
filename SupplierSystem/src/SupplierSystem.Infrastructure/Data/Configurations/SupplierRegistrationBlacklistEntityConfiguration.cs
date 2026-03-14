using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierRegistrationBlacklistEntityConfiguration : IEntityTypeConfiguration<SupplierRegistrationBlacklist>
{
    public void Configure(EntityTypeBuilder<SupplierRegistrationBlacklist> builder)
    {
        builder.ToTable("supplier_registration_blacklist");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.BlacklistType).HasColumnName("blacklist_type");
        builder.Property(entity => entity.BlacklistValue).HasColumnName("blacklist_value");
        builder.Property(entity => entity.AddedBy).HasColumnName("added_by");
        builder.Property(entity => entity.AddedByName).HasColumnName("added_by_name");
        builder.Property(entity => entity.AddedAt).HasColumnName("added_at");
        builder.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        builder.Property(entity => entity.IsActive).HasColumnName("is_active");
    }
}
