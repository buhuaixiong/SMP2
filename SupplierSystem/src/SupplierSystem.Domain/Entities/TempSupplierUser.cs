namespace SupplierSystem.Domain.Entities;

public sealed class TempSupplierUser
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? ExpiresAt { get; set; }
    public string? LastLoginAt { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
}
