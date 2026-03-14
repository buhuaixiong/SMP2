using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupplierSystem.Domain.Entities;

namespace SupplierSystem.Infrastructure.Data.Configurations;

public sealed class ApprovalCommentEntityConfiguration : IEntityTypeConfiguration<ApprovalComment>
{
    public void Configure(EntityTypeBuilder<ApprovalComment> builder)
    {
        builder.ToTable("approval_comments");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ApprovalId).HasColumnName("approval_id");
        builder.Property(entity => entity.AuthorId).HasColumnName("author_id");
        builder.Property(entity => entity.AuthorName).HasColumnName("author_name");
        builder.Property(entity => entity.Content).HasColumnName("content");
        builder.Property(entity => entity.CreatedAt).HasColumnName("created_at");
    }
}
