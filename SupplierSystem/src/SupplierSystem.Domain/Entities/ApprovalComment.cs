namespace SupplierSystem.Domain.Entities;

public sealed class ApprovalComment
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string AuthorId { get; set; } = null!;
    public string AuthorName { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string? CreatedAt { get; set; }
}
