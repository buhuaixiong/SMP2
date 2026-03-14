using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class RfqExternalInvitationEntityConfiguration : IEntityTypeConfiguration<RfqExternalInvitation>
{
    public void Configure(EntityTypeBuilder<RfqExternalInvitation> builder)
    {
        builder.ToTable("rfq_external_invitations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.BidRoundId).HasColumnName("bid_round_id");
        builder.Property(entity => entity.Email).HasColumnName("email");
        builder.Property(entity => entity.CompanyName).HasColumnName("company_name");
        builder.Property(entity => entity.ContactPerson).HasColumnName("contact_person");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.InvitedAt).HasColumnName("invited_at");
        builder.Property(entity => entity.RespondedAt).HasColumnName("responded_at");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.Token).HasColumnName("token");
        builder.Property(entity => entity.TokenExpiresAt).HasColumnName("token_expires_at");
        builder.Property(entity => entity.CreatedBy).HasColumnName("created_by");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
    }
}
