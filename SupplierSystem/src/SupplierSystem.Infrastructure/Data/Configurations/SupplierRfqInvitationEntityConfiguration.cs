using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class SupplierRfqInvitationEntityConfiguration : IEntityTypeConfiguration<SupplierRfqInvitation>
{
    public void Configure(EntityTypeBuilder<SupplierRfqInvitation> builder)
    {
        builder.ToTable("supplier_rfq_invitations");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RfqId).HasColumnName("rfq_id");
        builder.Property(entity => entity.BidRoundId).HasColumnName("bid_round_id");
        builder.Property(entity => entity.SupplierId).HasColumnName("supplier_id");
        builder.Property(entity => entity.InvitedAt).HasColumnName("invited_at");
        builder.Property(entity => entity.RespondedAt).HasColumnName("responded_at");
        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.Notes).HasColumnName("notes");
        builder.Property(entity => entity.InvitationToken).HasColumnName("invitation_token");
        builder.Property(entity => entity.TokenExpiresAt).HasColumnName("token_expires_at");
        builder.Property(entity => entity.RecipientEmail).HasColumnName("recipient_email");
        builder.Property(entity => entity.IsExternal).HasColumnName("is_external");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
    }
}
