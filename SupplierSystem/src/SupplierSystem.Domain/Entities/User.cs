namespace SupplierSystem.Domain.Entities;

public sealed class User
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Password { get; set; } = null!;
    public int AuthVersion { get; set; } = 1;
    public int? SupplierId { get; set; }
    public string? TenantId { get; set; }
    public string? Email { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public bool ForcePasswordReset { get; set; }
    public string? InitialPasswordIssuedAt { get; set; }
    public int? TempAccountId { get; set; }
    public string? AccountType { get; set; }
    public int? RelatedApplicationId { get; set; }
    public string? CreatedAt { get; set; }
    public string? UpdatedAt { get; set; }
    public string? Department { get; set; }
    public string? Status { get; set; }
    public string? LastLoginAt { get; set; }
}
