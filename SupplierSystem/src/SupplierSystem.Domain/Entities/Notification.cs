namespace SupplierSystem.Domain.Entities;

public sealed class Notification
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public string? UserId { get; set; }
    public string Type { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Priority { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? RelatedEntityType { get; set; }
    public int? RelatedEntityId { get; set; }
    public string CreatedAt { get; set; } = null!;
    public string? ReadAt { get; set; }
    public string? ExpiresAt { get; set; }
    public string? Metadata { get; set; }
}
