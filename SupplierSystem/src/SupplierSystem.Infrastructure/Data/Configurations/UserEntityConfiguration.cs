using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class UserEntityConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        var dateTimeStringConverter = new ValueConverter<string?, DateTime?>(
            value => string.IsNullOrWhiteSpace(value)
                ? null
                : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            value => value.HasValue
                ? value.Value.ToString("o", CultureInfo.InvariantCulture)
                : null);
        var nullableIntToLongConverter = new ValueConverter<int?, long?>(
            value => value.HasValue ? value.Value : null,
            value => value.HasValue ? checked((int)value.Value) : null);

        builder.ToTable("users", table => table.HasTrigger("trg_dbo_users_audit_update"));
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnName("id");
        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("username");
        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("role");
        builder.Property(u => u.Password)
            .IsRequired()
            .HasMaxLength(255)
            .HasColumnName("password_hash");
        builder.Property(u => u.AuthVersion)
            .HasColumnName("auth_version");

        builder.Property(u => u.Email)
            .HasMaxLength(320)
            .HasColumnName("email");
        builder.Property(u => u.TenantId)
            .HasMaxLength(64)
            .HasColumnName("tenant_id");
        builder.Property(u => u.AccountType)
            .HasMaxLength(50)
            .HasColumnName("account_type");
        builder.Property(u => u.CreatedAt)
            .HasMaxLength(64)
            .HasColumnName("created_at")
            .HasConversion(dateTimeStringConverter);
        builder.Property(u => u.UpdatedAt)
            .HasMaxLength(64)
            .HasColumnName("updated_at")
            .HasConversion(dateTimeStringConverter);
        builder.Property(u => u.Department)
            .HasMaxLength(100)
            .HasColumnName("department");
        builder.Property(u => u.Status)
            .HasMaxLength(50)
            .HasColumnName("status");
        builder.Property(u => u.LastLoginAt)
            .HasMaxLength(64)
            .HasColumnName("last_login_at")
            .HasConversion(dateTimeStringConverter);
        builder.Property(u => u.InitialPasswordIssuedAt)
            .HasMaxLength(64)
            .HasColumnName("initial_password_issued_at");

        builder.Property(u => u.SupplierId)
            .HasColumnName("supplier_id")
            .HasConversion(nullableIntToLongConverter);
        builder.Property(u => u.MustChangePassword)
            .HasColumnName("must_change_password");
        builder.Property(u => u.ForcePasswordReset)
            .HasColumnName("force_password_reset");
        builder.Property(u => u.TempAccountId)
            .HasColumnName("temp_account_id")
            .HasConversion(nullableIntToLongConverter);
        builder.Property(u => u.RelatedApplicationId)
            .HasColumnName("related_application_id")
            .HasConversion(nullableIntToLongConverter);

        builder.HasIndex(u => u.Email);
        builder.HasIndex(u => u.Username);
    }
}
