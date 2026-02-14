namespace SupplierSystem.Domain.Entities;

public sealed class TokenBlacklistEntry
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = null!;
    public string? UserId { get; set; }
    public string BlacklistedAt { get; set; } = null!;
    public string ExpiresAt { get; set; } = null!;
    public string? Reason { get; set; }
}
