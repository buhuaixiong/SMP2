namespace SupplierSystem.Domain.Entities;

public sealed class SupplierRegistrationBlacklist
{
    public int Id { get; set; }
    public string BlacklistType { get; set; } = null!;
    public string BlacklistValue { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string? Severity { get; set; }
    public string AddedBy { get; set; } = null!;
    public string? AddedByName { get; set; }
    public string? AddedAt { get; set; }
    public string? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
