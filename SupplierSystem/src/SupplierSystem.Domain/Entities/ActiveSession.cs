namespace SupplierSystem.Domain.Entities;

public sealed class ActiveSession
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public string IssuedAt { get; set; } = null!;
    public string ExpiresAt { get; set; } = null!;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedAt { get; set; }
}
